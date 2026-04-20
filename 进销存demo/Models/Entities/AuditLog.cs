using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    /// <summary>
    /// 标记接口：被此接口标记的实体会在 SaveChanges 时被自动写入 AuditLog 审计表。
    /// </summary>
    public interface IAuditLogged { }

    public enum AuditAction
    {
        Insert = 1,
        Update = 2,
        Delete = 3
    }

    /// <summary>
    /// 审计日志条目：谁、何时、对哪张表的哪条记录做了什么，变更字段及前后值（JSON 字符串）。
    /// </summary>
    public class AuditLog
    {
        public long Id { get; set; }

        [StringLength(64)]
        public string UserName { get; set; } = "system";

        [StringLength(64)]
        public string EntityType { get; set; } = string.Empty;

        [StringLength(64)]
        public string EntityKey { get; set; } = string.Empty;

        public AuditAction Action { get; set; }

        /// <summary>字段级变更，JSON：{"field":{"from":...,"to":...}}</summary>
        public string? Changes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(64)]
        public string? IpAddress { get; set; }
    }
}
