using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    public class PaymentVoucher : IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        [Required, StringLength(32), Display(Name = "付款单号")]
        public string OrderNo { get; set; } = "";

        public int PayableId { get; set; }
        public Payable? Payable { get; set; }

        [Display(Name = "金额")]
        public decimal Amount { get; set; }

        [Display(Name = "付款日期")]
        public DateTime PaidDate { get; set; } = DateTime.Today;

        [Display(Name = "方式")]
        public PaymentMethod Method { get; set; } = PaymentMethod.Bank;

        [StringLength(200), Display(Name = "备注")]
        public string? Remark { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "更新时间")]
        public DateTime? UpdatedAt { get; set; }
    }
}
