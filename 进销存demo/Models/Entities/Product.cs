using System.ComponentModel.DataAnnotations;
using 进销存demo.Models.Validation;

namespace 进销存demo.Models.Entities
{
    public class Product : ISoftDelete, IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        [Required, StringLength(32), Display(Name = "商品编码")]
        public string Code { get; set; } = string.Empty;

        [Required, StringLength(64), Display(Name = "商品名称")]
        public string Name { get; set; } = string.Empty;

        [StringLength(16), Display(Name = "单位")]
        public string Unit { get; set; } = "个";

        [StringLength(32), Display(Name = "条形码")]
        public string? Barcode { get; set; }

        [Display(Name = "分类")]
        public int? CategoryId { get; set; }
        public ProductCategory? Category { get; set; }

        [Range(typeof(decimal), "0", "999999.99", ErrorMessage = "采购价须在 0～999999.99 之间")]
        [Display(Name = "采购价")]
        public decimal PurchasePrice { get; set; }

        [Range(typeof(decimal), "0", "999999.99", ErrorMessage = "销售价须在 0～999999.99 之间")]
        [CompareGreaterThanOrEqual(nameof(PurchasePrice), ErrorMessage = "销售价不能低于采购价")]
        [Display(Name = "销售价")]
        public decimal SalePrice { get; set; }

        [Display(Name = "当前库存")]
        public int Stock { get; set; }

        [Display(Name = "安全库存")]
        public int SafetyStock { get; set; }

        [Display(Name = "启用")]
        public bool IsActive { get; set; } = true;

        [StringLength(200), Display(Name = "备注")]
        public string? Remark { get; set; }

        [Display(Name = "保质期(天)")]
        public int ShelfLifeDays { get; set; } = 365;

        [Display(Name = "启用批次/效期")]
        public bool TrackBatch { get; set; }

        // 乐观锁（SQLite 下 [Timestamp] 不被原生支持，用一个长整型 + IsRowVersion 代替，见 AppDbContext）
        [Display(Name = "版本")]
        public long RowVersion { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "更新时间")]
        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }

    public class ProductCategory : ISoftDelete, IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        [Required, StringLength(32), Display(Name = "分类名称")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200), Display(Name = "备注")]
        public string? Remark { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
    }
}
