using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public class OrderNoGenerator : IOrderNoGenerator
    {
        private readonly AppDbContext _db;
        public OrderNoGenerator(AppDbContext db) { _db = db; }

        /// <summary>
        /// 调用方应该已经开启事务（IDbContextTransaction）。SQLite 在事务第一次写操作时
        /// 会升级到 RESERVED/EXCLUSIVE 锁，天然串行化并发取号，不会跳号也不会重复。
        /// </summary>
        public async Task<string> NextAsync(string prefix, DateTime? date = null, CancellationToken ct = default)
        {
            var day = (date ?? DateTime.Today).ToString("yyyyMMdd");
            var scope = $"{prefix}-{day}";

            var seq = await _db.SequenceNumbers.FirstOrDefaultAsync(s => s.Scope == scope, ct);

            int value;
            if (seq == null)
            {
                value = 1;
                _db.SequenceNumbers.Add(new SequenceNumber
                {
                    Scope = scope,
                    NextValue = value + 1,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                value = seq.NextValue;
                seq.NextValue = value + 1;
                seq.UpdatedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync(ct);
            return $"{prefix}{day}-{value:D4}";
        }
    }
}
