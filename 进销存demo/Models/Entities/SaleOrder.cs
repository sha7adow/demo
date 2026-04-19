using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    public class SaleOrder
    {
        public int Id { get; set; }

        [Required, StringLength(32), Display(Name = "销售单号")]
        public string OrderNo { get; set; } = string.Empty;

        [Display(Name = "客户")]
        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

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

        public List<SaleOrderItem> Items { get; set; } = new();
    }

    public class SaleOrderItem
    {
        public int Id { get; set; }

        public int SaleOrderId { get; set; }
        public SaleOrder? SaleOrder { get; set; }

        [Display(Name = "商品")]
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        [Display(Name = "数量")]
        public int Quantity { get; set; }

        [Display(Name = "单价")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "小计")]
        public decimal Subtotal => Quantity * UnitPrice;
    }
}
