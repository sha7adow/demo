using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface IStocktakeService
    {
        Task<List<Stocktake>> ListAsync();
        Task<Stocktake?> GetAsync(int id);

        /// <summary>新建盘点单：快照当前所有启用商品的 SystemQty，实盘数默认等于 SystemQty。</summary>
        Task<Stocktake> CreateAsync(string? remark);

        /// <summary>保存用户录入的实盘数（仅草稿状态可保存）。</summary>
        Task UpdateActualQtyAsync(int stocktakeId, Dictionary<int, int> actualQtyByProductId);

        /// <summary>确认盘点：对差异项生成库存调整流水，并把单据置为 Confirmed。</summary>
        Task ConfirmAsync(int id);

        Task CancelAsync(int id);
        Task<string> GenerateOrderNoAsync();
    }
}
