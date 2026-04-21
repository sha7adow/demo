using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models;
using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public class BatchInventoryService : IBatchInventoryService
    {
        private static readonly JsonSerializerOptions JsonOpt = new() { PropertyNameCaseInsensitive = true };
        private readonly AppDbContext _db;

        public BatchInventoryService(AppDbContext db) => _db = db;

        public async Task<ProductBatch> ReceiveAsync(
            int productId,
            int qty,
            decimal unitCost,
            string? batchNo,
            DateTime productionDate,
            DateTime expiryDate,
            string? refNo,
            int? purchaseOrderItemId,
            StockChangeType stockChangeType = StockChangeType.Purchase,
            CancellationToken ct = default)
        {
            if (qty <= 0) throw new InvalidOperationException("入库数量必须大于 0");

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new InvalidOperationException($"商品不存在：{productId}");
            if (!product.TrackBatch)
                throw new InvalidOperationException($"商品未启用批次：{product.Name}");

            var no = string.IsNullOrWhiteSpace(batchNo)
                ? await GenerateBatchNoAsync(productId, product.Code, ct)
                : batchNo.Trim();

            if (await _db.ProductBatches.AnyAsync(b => b.ProductId == productId && b.BatchNo == no, ct))
                throw new InvalidOperationException($"批次号已存在：{no}");

            var batch = new ProductBatch
            {
                BatchNo = no,
                ProductId = productId,
                ProductionDate = productionDate.Date,
                ExpiryDate = expiryDate.Date,
                InitialQty = qty,
                RemainingQty = qty,
                UnitCost = unitCost,
                PurchaseOrderItemId = purchaseOrderItemId,
                CreatedAt = DateTime.Now
            };
            _db.ProductBatches.Add(batch);
            await _db.SaveChangesAsync(ct); // 拿到 Batch.Id

            await SyncProductStockFromBatchesAsync(productId, ct);

            var stockAfter = product.Stock;
            _db.StockTransactions.Add(new StockTransaction
            {
                ProductId = productId,
                BatchId = batch.Id,
                ChangeType = stockChangeType,
                Quantity = qty,
                StockAfter = stockAfter,
                RefOrderNo = refNo,
                Remark = stockChangeType switch
                {
                    StockChangeType.Purchase => refNo == null ? "采购入库" : $"采购入库 #{refNo}",
                    StockChangeType.Stocktake => refNo == null ? "盘点" : $"盘点 #{refNo}",
                    StockChangeType.Adjust => refNo == null ? "库存调整" : $"库存调整 #{refNo}",
                    _ => refNo
                },
                CreatedAt = DateTime.Now
            });

            await _db.SaveChangesAsync(ct);
            return batch;
        }

        public Task<IReadOnlyList<(int BatchId, int Qty)>> IssueFifoAsync(
            int productId,
            int qty,
            string? refNo,
            CancellationToken ct = default) =>
            IssueFifoAsync(productId, qty, refNo, "FIFO出库", ct);

        public async Task<IReadOnlyList<(int BatchId, int Qty)>> IssueFifoAsync(
            int productId,
            int qty,
            string? refNo,
            string remark,
            CancellationToken ct = default)
        {
            if (qty <= 0) throw new InvalidOperationException("出库数量必须大于 0");

            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new InvalidOperationException($"商品不存在：{productId}");
            if (!product.TrackBatch)
                throw new InvalidOperationException($"商品未启用批次：{product.Name}");

            var today = DateTime.Today;
            var batches = await _db.ProductBatches
                .Where(b => b.ProductId == productId && b.RemainingQty > 0 && b.ExpiryDate.Date >= today)
                .OrderBy(b => b.ExpiryDate)
                .ThenBy(b => b.Id)
                .ToListAsync(ct);

            var available = batches.Sum(b => b.RemainingQty);
            if (available < qty)
                throw new InvalidOperationException(
                    $"可售批次库存不足（不含已过期）：{product.Name} 可出 {available}，需出 {qty}");

            var allocations = new List<(int BatchId, int Qty)>();
            var need = qty;
            foreach (var b in batches)
            {
                if (need <= 0) break;
                var take = Math.Min(need, b.RemainingQty);
                if (take <= 0) continue;
                b.RemainingQty -= take;
                need -= take;
                allocations.Add((b.Id, take));

                await SyncProductStockFromBatchesAsync(productId, ct);
                var stockAfter = product.Stock;
                _db.StockTransactions.Add(new StockTransaction
                {
                    ProductId = productId,
                    BatchId = b.Id,
                    ChangeType = StockChangeType.Sale,
                    Quantity = -take,
                    StockAfter = stockAfter,
                    RefOrderNo = refNo,
                    Remark = remark,
                    CreatedAt = DateTime.Now
                });
                await _db.SaveChangesAsync(ct);
            }

            if (need > 0)
                throw new InvalidOperationException("批次扣减异常，请重试");

            return allocations;
        }

        public async Task<IReadOnlyList<ProductBatch>> ListExpiringAsync(int daysAhead = 30, CancellationToken ct = default)
        {
            var today = DateTime.Today;
            var end = today.AddDays(daysAhead);
            var list = await _db.ProductBatches
                .Include(b => b.Product)
                .Where(b => b.RemainingQty > 0 && b.ExpiryDate.Date > today && b.ExpiryDate.Date <= end)
                .OrderBy(b => b.ExpiryDate)
                .ToListAsync(ct);
            return list;
        }

        public async Task ReturnPurchaseItemBatchesAsync(
            int purchaseOrderItemId,
            int qty,
            string? refNo,
            string remark,
            CancellationToken ct = default)
        {
            if (qty <= 0) return;

            var batches = await _db.ProductBatches
                .Where(b => b.PurchaseOrderItemId == purchaseOrderItemId && b.RemainingQty > 0)
                .OrderByDescending(b => b.ExpiryDate)
                .ThenByDescending(b => b.Id)
                .ToListAsync(ct);

            var productId = batches.Select(b => b.ProductId).FirstOrDefault();
            if (!productId.HasValue)
                throw new InvalidOperationException("找不到该采购明细对应的批次，无法退货冲减");

            var can = batches.Sum(b => b.RemainingQty);
            if (can < qty)
                throw new InvalidOperationException($"批次可退数量不足：剩余 {can}，需退 {qty}");

            var need = qty;
            foreach (var b in batches)
            {
                if (need <= 0) break;
                var take = Math.Min(need, b.RemainingQty);
                b.RemainingQty -= take;
                need -= take;

                var pid = productId.Value;
                var product = await _db.Products.FirstAsync(p => p.Id == pid, ct);
                await SyncProductStockFromBatchesAsync(pid, ct);
                _db.StockTransactions.Add(new StockTransaction
                {
                    ProductId = pid,
                    BatchId = b.Id,
                    ChangeType = StockChangeType.PurchaseReturn,
                    Quantity = -take,
                    StockAfter = product.Stock,
                    RefOrderNo = refNo,
                    Remark = remark,
                    CreatedAt = DateTime.Now
                });
                await _db.SaveChangesAsync(ct);
            }
        }

        public async Task ReturnSaleConsumedBatchesAsync(
            string? consumedFromBatchesJson,
            int productId,
            string? refNo,
            string remark,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(consumedFromBatchesJson)) return;

            List<BatchConsumeEntry>? list;
            try
            {
                list = JsonSerializer.Deserialize<List<BatchConsumeEntry>>(consumedFromBatchesJson, JsonOpt);
            }
            catch
            {
                throw new InvalidOperationException("销售明细上的批次消耗记录格式无效，无法退货回补");
            }

            if (list == null || list.Count == 0) return;

            foreach (var e in list)
            {
                if (e.Qty <= 0) continue;
                var batch = await _db.ProductBatches.FirstOrDefaultAsync(b => b.Id == e.BatchId && b.ProductId == productId, ct)
                    ?? throw new InvalidOperationException($"批次不存在：{e.BatchId}");
                batch.RemainingQty += e.Qty;
            }

            await SyncProductStockFromBatchesAsync(productId, ct);
            var product = await _db.Products.FirstAsync(p => p.Id == productId, ct);
            var totalQty = list.Where(x => x.Qty > 0).Sum(x => x.Qty);
            _db.StockTransactions.Add(new StockTransaction
            {
                ProductId = productId,
                BatchId = null,
                ChangeType = StockChangeType.SaleReturn,
                Quantity = totalQty,
                StockAfter = product.Stock,
                RefOrderNo = refNo,
                Remark = remark,
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync(ct);
        }

        private async Task SyncProductStockFromBatchesAsync(int productId, CancellationToken ct)
        {
            var sum = await _db.ProductBatches.Where(b => b.ProductId == productId).SumAsync(b => b.RemainingQty, ct);
            var product = await _db.Products.FirstAsync(p => p.Id == productId, ct);
            product.Stock = sum;
            await _db.SaveChangesAsync(ct);
        }

        private async Task<string> GenerateBatchNoAsync(int productId, string productCode, CancellationToken ct)
        {
            var day = DateTime.Today.ToString("yyyyMMdd");
            var prefix = $"{productCode}-{day}-";
            var n = await _db.ProductBatches.CountAsync(b => b.ProductId == productId && b.BatchNo.StartsWith(prefix), ct);
            return $"{prefix}{(n + 1):D4}";
        }
    }
}
