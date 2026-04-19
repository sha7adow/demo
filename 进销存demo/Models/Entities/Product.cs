using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    public class Product
    {
        public int Id { get; set; }

        [Required, StringLength(32), Display(Name = "商品编码")]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(64), Display(Name = "商品名称")]
        public string Name { get; set; } = string.Empty;

        [StringLength(16), Display(Name = "单位")]
        public string Unit { get; set; } = "个";

        [Display(Name = "采购价")]
        public decimal PurchasePrice { get; set; }

        [Display(Name = "销售价")]
        public decimal SalePrice { get; set; }

        [Display(Name = "当前库存")]
        public int Stock { get; set; }

        [Display(Name = "安全库存")]
        public int SafetyStock { get; set; }

        [StringLength(200), Display(Name = "备注")]
        public string? Remark { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
