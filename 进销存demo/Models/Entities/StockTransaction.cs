using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    /// <summary>
    /// 库存流水：每一次出/入库都会写一条记录，用于追溯。
    /// Quantity 为正表示入库，为负表示出库。
    /// </summary>
    public class StockTransaction
    {
        public int Id { get; set; }

        [Display(Name = "商品")]
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        public int? BatchId { get; set; }
        public ProductBatch? Batch { get; set; }

        [Display(Name = "类型")]
        public StockChangeType ChangeType { get; set; }

        [Display(Name = "数量变化")]
        public int Quantity { get; set; }

        [Display(Name = "变动后库存")]
        public int StockAfter { get; set; }

        [StringLength(32), Display(Name = "关联单号")]
        public string? RefOrderNo { get; set; }

        [StringLength(200), Display(Name = "备注")]
        public string? Remark { get; set; }

        [Display(Name = "时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
