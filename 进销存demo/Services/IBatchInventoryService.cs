using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface IBatchInventoryService
    {
        Task<ProductBatch> ReceiveAsync(
            int productId,
            int qty,
            decimal unitCost,
            string? batchNo,
            DateTime productionDate,
            DateTime expiryDate,
            string? refNo,
            int? purchaseOrderItemId,
            StockChangeType stockChangeType = StockChangeType.Purchase,
            CancellationToken ct = default);

        Task<IReadOnlyList<(int BatchId, int Qty)>> IssueFifoAsync(
            int productId,
            int qty,
            string? refNo,
            CancellationToken ct = default);

        Task<IReadOnlyList<(int BatchId, int Qty)>> IssueFifoAsync(
            int productId,
            int qty,
            string? refNo,
            string remark,
            CancellationToken ct = default);

        Task<IReadOnlyList<ProductBatch>> ListExpiringAsync(int daysAhead = 30, CancellationToken ct = default);

        /// <summary>采购退货：按该采购明细关联的批次扣减剩余量。</summary>
        Task ReturnPurchaseItemBatchesAsync(
            int purchaseOrderItemId,
            int qty,
            string? refNo,
            string remark,
            CancellationToken ct = default);

        /// <summary>销售退货：按 ConsumedFromBatches JSON 回补批次。</summary>
        Task ReturnSaleConsumedBatchesAsync(
            string? consumedFromBatchesJson,
            int productId,
            string? refNo,
            string remark,
            CancellationToken ct = default);
    }
}
