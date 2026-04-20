using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using 进销存demo.Models.Entities;

namespace 进销存demo.Data
{
    /// <summary>
    /// 保存前/后统一处理：
    ///   1) 审计字段 CreatedAt / UpdatedAt
    ///   2) 软删除：Deleted 物理删除转为 Modified + 置 IsDeleted
    ///   3) 乐观锁：Product.RowVersion 每次修改 +1
    ///   4) AuditLog：对实现 <see cref="IAuditLogged"/> 的实体记录 Insert/Update/Delete 一行日志（含字段变更 JSON）
    /// </summary>
    public class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly IHttpContextAccessor? _http;

        // 每个 DbContext 一份待写入的 AuditLog 暂存（EF 会同一线程/异步链重入 SavingChanges）
        private readonly ConcurrentDictionary<DbContext, List<PendingAudit>> _pending = new();

        // 阻止拦截器在写 AuditLog 时再次递归
        private static readonly AsyncLocal<bool> _writingAudit = new();

        private static readonly JsonSerializerOptions _json = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public AuditSaveChangesInterceptor(IHttpContextAccessor? http = null)
        {
            _http = http;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData, InterceptionResult<int> result)
        {
            OnSaving(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            OnSaving(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            OnSavedAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
            return base.SavedChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            await OnSavedAsync(eventData.Context, cancellationToken);
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        // ---------------- 元数据：审计字段 / 软删除 / RowVersion ----------------
        private void OnSaving(DbContext? ctx)
        {
            if (ctx == null) return;
            var now = DateTime.Now;

            var pending = new List<PendingAudit>();
            bool capture = !_writingAudit.Value;

            foreach (var entry in ctx.ChangeTracker.Entries())
            {
                // 审计字段
                if (entry.Entity is IAuditable auditable)
                {
                    if (entry.State == EntityState.Added)
                    {
                        if (auditable.CreatedAt == default) auditable.CreatedAt = now;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditable.UpdatedAt = now;
                    }
                }

                // 软删除
                if (entry.Entity is ISoftDelete softDelete && entry.State == EntityState.Deleted)
                {
                    entry.State = EntityState.Modified;
                    softDelete.IsDeleted = true;
                    softDelete.DeletedAt = now;
                }

                // Product 乐观锁
                if (entry.Entity is Product product && entry.State == EntityState.Modified)
                {
                    product.RowVersion += 1;
                }

                if (capture && entry.Entity is IAuditLogged)
                {
                    var action = entry.State switch
                    {
                        EntityState.Added => AuditAction.Insert,
                        EntityState.Modified => entry.Entity is ISoftDelete sd && sd.IsDeleted && sd.DeletedAt == now
                            ? AuditAction.Delete
                            : AuditAction.Update,
                        EntityState.Deleted => AuditAction.Delete,
                        _ => (AuditAction?)null
                    };
                    if (action.HasValue)
                    {
                        pending.Add(new PendingAudit
                        {
                            Entry = entry,
                            Action = action.Value,
                            Changes = BuildChanges(entry, action.Value)
                        });
                    }
                }
            }

            if (capture && pending.Count > 0)
            {
                _pending[ctx] = pending;
            }
        }

        // ---------------- 保存后：把审计日志追加到 AuditLogs ----------------
        private async Task OnSavedAsync(DbContext? ctx, CancellationToken ct)
        {
            if (ctx == null) return;
            if (!_pending.TryRemove(ctx, out var pending) || pending.Count == 0) return;

            var user = GetUserName();
            var ip = _http?.HttpContext?.Connection?.RemoteIpAddress?.ToString();
            var now = DateTime.Now;

            foreach (var p in pending)
            {
                ctx.Add(new AuditLog
                {
                    UserName = user,
                    EntityType = p.Entry.Entity.GetType().Name,
                    EntityKey = GetPrimaryKey(p.Entry),
                    Action = p.Action,
                    Changes = p.Changes,
                    CreatedAt = now,
                    IpAddress = ip
                });
            }

            _writingAudit.Value = true;
            try { await ctx.SaveChangesAsync(ct); }
            finally { _writingAudit.Value = false; }
        }

        // ---------------- 辅助 ----------------
        private static string GetPrimaryKey(EntityEntry entry)
        {
            var pk = entry.Metadata.FindPrimaryKey();
            if (pk == null) return string.Empty;
            var parts = pk.Properties.Select(p => entry.CurrentValues[p]?.ToString() ?? "").ToArray();
            return string.Join(",", parts);
        }

        private static string? BuildChanges(EntityEntry entry, AuditAction action)
        {
            try
            {
                var dict = new Dictionary<string, object?>();
                switch (action)
                {
                    case AuditAction.Insert:
                        foreach (var p in entry.Properties)
                        {
                            if (p.Metadata.IsPrimaryKey()) continue;
                            dict[p.Metadata.Name] = new { to = p.CurrentValue };
                        }
                        break;
                    case AuditAction.Update:
                        foreach (var p in entry.Properties)
                        {
                            if (p.Metadata.IsPrimaryKey()) continue;
                            if (!Equals(p.OriginalValue, p.CurrentValue))
                                dict[p.Metadata.Name] = new { from = p.OriginalValue, to = p.CurrentValue };
                        }
                        if (dict.Count == 0) return null;
                        break;
                    case AuditAction.Delete:
                        foreach (var p in entry.Properties)
                        {
                            if (p.Metadata.IsPrimaryKey()) continue;
                            dict[p.Metadata.Name] = new { from = p.OriginalValue };
                        }
                        break;
                }
                return JsonSerializer.Serialize(dict, _json);
            }
            catch
            {
                return null;
            }
        }

        private string GetUserName()
        {
            var user = _http?.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(user.Identity.Name))
                return user.Identity.Name;
            return "system";
        }

        private sealed class PendingAudit
        {
            public EntityEntry Entry { get; set; } = null!;
            public AuditAction Action { get; set; }
            public string? Changes { get; set; }
        }
    }
}
