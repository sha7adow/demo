using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 进销存demo.Data;
using 进销存demo.Models;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Options;

namespace 进销存demo.Services
{
    public class SaleService : ISaleService
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IBatchInventoryService _batch;
        private readonly IOrderNoGenerator _orderNo;
        private readonly string _prefix;

        public SaleService(
            AppDbContext db,
            IInventoryService inventory,
            IBatchInventoryService batch,
            IOrderNoGenerator orderNo,
            IOptions<JxcOptions> options)
        {
            _db = db;
            _inventory = inventory;
            _batch = batch;
            _orderNo = orderNo;
            _prefix = options.Value.OrderPrefix.Sale;
        }

        public async Task<string> GenerateOrderNoAsync()
        {
            var day = DateTime.Today.ToString("yyyyMMdd");
            var scope = $"{_prefix}-{day}";
            var seq = await _db.SequenceNumbers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Scope == scope);
            var preview = seq?.NextValue ?? 1;
            return $"{_prefix}{day}-{preview:D4}";
        }

        public async Task<List<SaleOrder>> ListAsync() =>
            await _db.SaleOrders
                .Include(o => o.Customer)
                .OrderByDescending(o => o.Id)
                .ToListAsync();

        public async Task<SaleOrder?> GetAsync(int id) =>
            await _db.SaleOrders
                .Include(o => o.Customer)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

        public async Task<SaleOrder> CreateAsync(SaleOrder order)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            order.OrderNo = await _orderNo.NextAsync(_prefix, order.OrderDate);
            order.Status = OrderStatus.Draft;
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            order.CreatedAt = DateTime.Now;

            _db.SaleOrders.Add(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return order;
        }

        public async Task<int> CreateManyFromImportAsync(IReadOnlyList<SaleOrder> orders, CancellationToken ct = default)
        {
            if (orders.Count == 0) return 0;
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            foreach (var order in orders)
            {
                var no = (order.OrderNo ?? "").Trim();
                if (string.IsNullOrEmpty(no))
                    throw new InvalidOperationException("导入失败：存在空单号");
                if (await _db.SaleOrders.AnyAsync(o => o.OrderNo == no, ct))
                    throw new InvalidOperationException($"导入失败：单号已存在「{no}」");
                await _orderNo.SyncAfterManualOrderNoAsync(no, _prefix, ct);
                order.OrderNo = no;
                order.Status = OrderStatus.Draft;
                order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
                order.CreatedAt = DateTime.Now;
                _db.SaleOrders.Add(order);
                await _db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return orders.Count;
        }

        public async Task ConfirmAsync(int id)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.SaleOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id)
                    ?? throw new InvalidOperationException("销售单不存在");

                if (order.Status != OrderStatus.Draft)
                    throw new InvalidOperationException("仅草稿状态的销售单可以确认出库");

                if (order.Items.Count == 0)
                    throw new InvalidOperationException("销售单没有明细，无法出库");
                if (!order.CustomerId.HasValue)
                    throw new InvalidOperationException("销售单缺少客户，无法出库");
                if (order.Items.Any(i => !i.ProductId.HasValue))
                    throw new InvalidOperationException("销售明细存在缺少商品的行");

                // 聚合校验：同商品多明细汇总后再判断库存
                var grouped = order.Items
                    .GroupBy(i => i.ProductId!.Value)
                    .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                    .ToList();

                var productIds = grouped.Select(g => g.ProductId).ToList();
                var products = await _db.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                foreach (var g in grouped)
                {
                    if (!products.TryGetValue(g.ProductId, out var p))
                        throw new InvalidOperationException($"商品不存在：{g.ProductId}");
                    if (p.TrackBatch)
                    {
                        var today = DateTime.Today;
                        var avail = await _db.ProductBatches
                            .Where(b => b.ProductId == g.ProductId && b.RemainingQty > 0 && b.ExpiryDate.Date >= today)
                            .SumAsync(b => b.RemainingQty);
                        if (avail < g.Qty)
                            throw new InvalidOperationException(
                                $"可售批次库存不足（不含已过期）：{p.Name} 可出 {avail}，需出 {g.Qty}");
                    }
                    else if (p.Stock < g.Qty)
                    {
                        throw new InvalidOperationException($"库存不足：{p.Name} 当前 {p.Stock}，需出 {g.Qty}");
                    }
                }

                foreach (var item in order.Items)
                {
                    var pid = item.ProductId!.Value;
                    var prod = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == pid);
                    if (prod.TrackBatch)
                    {
                        var alloc = await _batch.IssueFifoAsync(
                            pid,
                            item.Quantity,
                            order.OrderNo,
                            $"销售出库 #{order.OrderNo}");
                        item.ConsumedFromBatches = JsonSerializer.Serialize(
                            alloc.Select(a => new BatchConsumeEntry { BatchId = a.BatchId, Qty = a.Qty }));
                    }
                    else
                    {
                        await _inventory.ApplyStockChangeAsync(
                            pid,
                            -item.Quantity,
                            StockChangeType.Sale,
                            order.OrderNo,
                            $"销售出库 #{order.OrderNo}");
                    }
                }

                order.Status = OrderStatus.Confirmed;
                order.ConfirmedAt = DateTime.Now;
                order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

                var customer = await _db.Customers.AsNoTracking().FirstAsync(c => c.Id == order.CustomerId.Value);
                if (!await _db.Receivables.AnyAsync(r => r.SaleOrderId == order.Id))
                {
                    _db.Receivables.Add(new Receivable
                    {
                        CustomerId = order.CustomerId.Value,
                        SaleOrderId = order.Id,
                        OrderNo = order.OrderNo,
                        Amount = order.TotalAmount,
                        Paid = 0,
                        DueDate = order.OrderDate.Date.AddDays(customer.PaymentTermDays),
                        Status = ReceivableStatus.Outstanding,
                        CreatedAt = DateTime.Now
                    });
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException("相关商品已被其他操作修改，请刷新后重试");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task ReturnAsync(int id, string? remark = null)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.SaleOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id)
                    ?? throw new InvalidOperationException("销售单不存在");

                if (order.Status != OrderStatus.Confirmed)
                    throw new InvalidOperationException("仅已确认出库的销售单可以退货");

                foreach (var item in order.Items)
                {
                    if (!item.ProductId.HasValue)
                        throw new InvalidOperationException("销售明细缺少商品");
                    var pid = item.ProductId.Value;
                    var prod = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == pid);
                    if (prod.TrackBatch && !string.IsNullOrWhiteSpace(item.ConsumedFromBatches))
                    {
                        await _batch.ReturnSaleConsumedBatchesAsync(
                            item.ConsumedFromBatches,
                            pid,
                            order.OrderNo,
                            string.IsNullOrWhiteSpace(remark)
                                ? $"销售退货 #{order.OrderNo}"
                                : $"销售退货 #{order.OrderNo}：{remark}");
                    }
                    else
                    {
                        await _inventory.ApplyStockChangeAsync(
                            pid,
                            item.Quantity,
                            StockChangeType.SaleReturn,
                            order.OrderNo,
                            string.IsNullOrWhiteSpace(remark)
                                ? $"销售退货 #{order.OrderNo}"
                                : $"销售退货 #{order.OrderNo}：{remark}");
                    }
                }

                var receivable = await _db.Receivables.FirstOrDefaultAsync(r => r.SaleOrderId == order.Id);
                if (receivable != null)
                {
                    receivable.Status = ReceivableStatus.WrittenOff;
                    receivable.UpdatedAt = DateTime.Now;
                }

                order.Status = OrderStatus.Returned;
                order.ReturnedAt = DateTime.Now;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException("相关商品已被其他操作修改，请刷新后重试");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task CancelAsync(int id)
        {
            var order = await _db.SaleOrders.FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new InvalidOperationException("销售单不存在");

            if (order.Status == OrderStatus.Confirmed)
                throw new InvalidOperationException("已确认出库的销售单不能取消，请使用退货或库存调整冲销");

            if (order.Status == OrderStatus.Returned)
                throw new InvalidOperationException("已退货的销售单无需取消");

            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var order = await _db.SaleOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return;

            if (order.Status is OrderStatus.Confirmed or OrderStatus.Returned)
                throw new InvalidOperationException("已出库/已退货的销售单不可删除");

            _db.SaleOrders.Remove(order);
            await _db.SaveChangesAsync();
        }
    }
}
