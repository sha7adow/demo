using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    public class Supplier : ISoftDelete, IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        [Required, StringLength(64), Display(Name = "供应商名称")]
        public string Name { get; set; } = string.Empty;

        [StringLength(32), Display(Name = "联系人")]
        public string? Contact { get; set; }

        [StringLength(32), Display(Name = "电话")]
        public string? Phone { get; set; }

        [StringLength(200), Display(Name = "地址")]
        public string? Address { get; set; }

        [StringLength(200), Display(Name = "备注")]
        public string? Remark { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "更新时间")]
        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
