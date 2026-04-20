using System.ComponentModel.DataAnnotations;

namespace 进销存demo.Models.Entities
{
    /// <summary>
    /// 盘点单：草稿时生成一组商品的「系统数 / 实盘数」明细；确认后按差异生成调整流水。
    /// </summary>
    public class Stocktake : IAuditable, IAuditLogged
    {
        public int Id { get; set; }

        [Required, StringLength(32), Display(Name = "盘点单号")]
        public string OrderNo { get; set; } = string.Empty;

        [Display(Name = "盘点日期")]
        public DateTime OrderDate { get; set; } = DateTime.Today;

        [Display(Name = "状态")]
        public StocktakeStatus Status { get; set; } = StocktakeStatus.Draft;

        [StringLength(200), Display(Name = "备注")]
        public string? Remark { get; set; }

        [Display(Name = "创建时间")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [Display(Name = "确认时间")]
        public DateTime? ConfirmedAt { get; set; }

        public List<StocktakeItem> Items { get; set; } = new();
    }

    public class StocktakeItem
    {
        public int Id { get; set; }

        public int StocktakeId { get; set; }
        public Stocktake? Stocktake { get; set; }

        public int ProductId { get; set; }
        public Product? Product { get; set; }

        /// <summary>草稿生成时快照的系统库存</summary>
        [Display(Name = "系统数")]
        public int SystemQty { get; set; }

        /// <summary>实盘数（由用户录入）</summary>
        [Display(Name = "实盘数")]
        public int ActualQty { get; set; }

        /// <summary>差异 = ActualQty - SystemQty，确认时生成此数量的流水</summary>
        [Display(Name = "差异")]
        public int Diff => ActualQty - SystemQty;
    }
}
