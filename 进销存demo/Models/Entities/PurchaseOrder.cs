using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    public class PurchaseOrder : IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        [Required, StringLength(32), Display(Name = "采购单号")]
        public string OrderNo { get; set; } = string.Empty;

        [Display(Name = "供应商")]
        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        [Display(Name = "下单日期")]
        public DateTime OrderDate { get; set; } = DateTime.Today;

        [Display(Name = "状态")]
        public OrderStatus Status { get; set; } = OrderStatus.Draft;

        [Display(Name = "总金额")]
        public decimal TotalAmount { get; set; }

        [StringLength(200), Display(Name = "备注")]
        public string? Remark { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "更新时间")]
        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "确认时间")]
        public DateTime? ConfirmedAt { get; set; }

        [Display(Name = "退货时间")]
        public DateTime? ReturnedAt { get; set; }

        public Payable? Payable { get; set; }

        public List<PurchaseOrderItem> Items { get; set; } = new();
    }

    public class PurchaseOrderItem
    {
        public int Id { get; set; }

        public int PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        [Display(Name = "商品")]
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        [Display(Name = "数量")]
        public int Quantity { get; set; }

        [Display(Name = "单价")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "小计")]
        public decimal Subtotal => Quantity * UnitPrice;

        [Display(Name = "生产日期")]
        public DateTime? ProductionDate { get; set; }

        [StringLength(32), Display(Name = "批次号")]
        public string? BatchNo { get; set; }
    }
}
