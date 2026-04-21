using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 进销存demo.Models.Entities
{
    public class Payable : IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        public int? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }

        public int PurchaseOrderId { get; set; }
        public PurchaseOrder? PurchaseOrder { get; set; }

        [StringLength(32), Display(Name = "单号")]
        public string OrderNo { get; set; } = "";

        [Display(Name = "应付总额")]
        public decimal Amount { get; set; }

        [Display(Name = "已付")]
        public decimal Paid { get; set; }

        [NotMapped]
        public decimal Balance => Amount - Paid;

        [Display(Name = "到期日")]
        public DateTime DueDate { get; set; }

        [Display(Name = "状态")]
        public PayableStatus Status { get; set; } = PayableStatus.Outstanding;

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "更新时间")]
        public DateTime? UpdatedAt { get; set; }

        public List<PaymentVoucher> Vouchers { get; set; } = new();
    }
}
