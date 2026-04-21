using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace 进销存demo.Models.Entities
{
    public class Receivable : IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        public int? CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int SaleOrderId { get; set; }
        public SaleOrder? SaleOrder { get; set; }

        [StringLength(32), Display(Name = "单号")]
        public string OrderNo { get; set; } = "";

        [Display(Name = "应收总额")]
        public decimal Amount { get; set; }

        [Display(Name = "已收")]
        public decimal Paid { get; set; }

        [NotMapped]
        public decimal Balance => Amount - Paid;

        [Display(Name = "到期日")]
        public DateTime DueDate { get; set; }

        [Display(Name = "状态")]
        public ReceivableStatus Status { get; set; } = ReceivableStatus.Outstanding;

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "更新时间")]
        public DateTime? UpdatedAt { get; set; }

        public List<PaymentReceipt> Receipts { get; set; } = new();
    }
}
