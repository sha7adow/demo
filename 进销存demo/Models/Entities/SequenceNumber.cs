using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    /// <summary>
    /// 单据按日流水号。Scope 形如 "PO-20260420" / "SO-20260420" / "ST-20260420"。
    /// 事务内 UPDATE ... SET NextValue = NextValue + 1 保证不跳号不重复。
    /// </summary>
    public class SequenceNumber
    {
        public int Id { get; set; }

        [Required, StringLength(32)]
        public string Scope { get; set; } = string.Empty;

        public int NextValue { get; set; } = 1;

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
