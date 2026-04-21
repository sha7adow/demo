using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    public class ProductBatch : IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        [Required, StringLength(32)]
        public string BatchNo { get; set; } = string.Empty;

        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        [Display(Name = "生产日期")]
        public DateTime ProductionDate { get; set; }

        [Display(Name = "到期日期")]
        public DateTime ExpiryDate { get; set; }

        [Display(Name = "入库数量")]
        public int InitialQty { get; set; }

        [Display(Name = "剩余可售")]
        public int RemainingQty { get; set; }

        [Display(Name = "入库单价")]
        public decimal UnitCost { get; set; }

        public int? PurchaseOrderItemId { get; set; }
        public PurchaseOrderItem? PurchaseOrderItem { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "更新时间")]
        public DateTime? UpdatedAt { get; set; }
    }
}
