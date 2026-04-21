using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _db;
        private readonly IBatchInventoryService _batch;

        public InventoryService(AppDbContext db, IBatchInventoryService batch)
        {
            _db = db;
            _batch = batch;
        }

        public async Task ApplyStockChangeAsync(
            int productId,
            int quantityDelta,
            StockChangeType type,
            string? refNo,
            string? remark = null,
            CancellationToken ct = default)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new InvalidOperationException($"商品不存在：{productId}");

            var newStock = product.Stock + quantityDelta;
            if (newStock < 0)
                throw new InvalidOperationException(
                    $"库存不足：{product.Name} 当前 {product.Stock}，本次需出 {-quantityDelta}");

            product.Stock = newStock;

            _db.StockTransactions.Add(new StockTransaction
            {
                ProductId = productId,
                ChangeType = type,
                Quantity = quantityDelta,
                StockAfter = newStock,
                RefOrderNo = refNo,
                Remark = remark,
                CreatedAt = DateTime.Now
            });
            // 注意：不调用 SaveChanges，由调用方事务统一提交
        }

        public async Task AdjustAsync(int productId, int quantityDelta, string? remark, CancellationToken ct = default)
        {
            if (productId <= 0) throw new InvalidOperationException("请选择商品");
            if (quantityDelta == 0) throw new InvalidOperationException("调整数量不能为 0");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                    ?? throw new InvalidOperationException("商品不存在");

                if (product.TrackBatch)
                {
                    if (quantityDelta > 0)
                    {
                        var pd = DateTime.Today;
                        var shelf = product.ShelfLifeDays <= 0 ? 365 : product.ShelfLifeDays;
                        var exp = pd.AddDays(shelf);
                        var refNo = $"ADJ-{productId}-{DateTime.Now:yyyyMMddHHmmss}";
                        await _batch.ReceiveAsync(
                            productId,
                            quantityDelta,
                            product.PurchasePrice,
                            batchNo: null,
                            pd,
                            exp,
                            refNo,
                            purchaseOrderItemId: null,
                            StockChangeType.Adjust,
                            ct);
                    }
                    else
                    {
                        await _batch.IssueFifoAsync(
                            productId,
                            -quantityDelta,
                            refNo: null,
                            string.IsNullOrWhiteSpace(remark) ? "手工调整" : remark,
                            ct);
                    }
                }
                else
                {
                    await ApplyStockChangeAsync(
                        productId,
                        quantityDelta,
                        StockChangeType.Adjust,
                        refNo: null,
                        remark: string.IsNullOrWhiteSpace(remark) ? "手工调整" : remark,
                        ct);

                    await _db.SaveChangesAsync(ct);
                }

                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                throw new InvalidOperationException("该商品正被其他操作修改，请刷新后重试");
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
    }
}
