using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface IInventoryService
    {
        /// <summary>
        /// 变动库存 + 写流水。<b>不</b>调用 SaveChanges；调用方负责事务提交。
        /// </summary>
        Task ApplyStockChangeAsync(
            int productId,
            int quantityDelta,
            StockChangeType type,
            string? refNo,
            string? remark = null,
            CancellationToken ct = default);

        /// <summary>库存调整（独立事务）：供 Inventory/Adjust 页面使用。</summary>
        Task AdjustAsync(
            int productId,
            int quantityDelta,
            string? remark,
            CancellationToken ct = default);
    }
}
