using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    public class Customer
    {
        public int Id { get; set; }

        [Required, StringLength(64), Display(Name = "客户名称")]
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
    }
}
