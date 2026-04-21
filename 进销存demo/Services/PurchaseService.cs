using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 进销存demo.Data;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Options;

namespace 进销存demo.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IBatchInventoryService _batch;
        private readonly IOrderNoGenerator _orderNo;
        private readonly string _prefix;

        public PurchaseService(
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
            _prefix = options.Value.OrderPrefix.Purchase;
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

        public async Task<List<PurchaseOrder>> ListAsync() =>
            await _db.PurchaseOrders
                .Include(o => o.Supplier)
                .OrderByDescending(o => o.Id)
                .ToListAsync();

        public async Task<PurchaseOrder?> GetAsync(int id) =>
            await _db.PurchaseOrders
                .Include(o => o.Supplier)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

        public async Task<PurchaseOrder> CreateAsync(PurchaseOrder order)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            order.OrderNo = await _orderNo.NextAsync(_prefix, order.OrderDate);
            order.Status = OrderStatus.Draft;
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            order.CreatedAt = DateTime.Now;

            _db.PurchaseOrders.Add(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return order;
        }

        public async Task<int> CreateManyFromImportAsync(IReadOnlyList<PurchaseOrder> orders, CancellationToken ct = default)
        {
            if (orders.Count == 0) return 0;
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            foreach (var order in orders)
            {
                var no = (order.OrderNo ?? "").Trim();
                if (string.IsNullOrEmpty(no))
                    throw new InvalidOperationException("导入失败：存在空单号");
                if (await _db.PurchaseOrders.AnyAsync(o => o.OrderNo == no, ct))
                    throw new InvalidOperationException($"导入失败：单号已存在「{no}」");
                await _orderNo.SyncAfterManualOrderNoAsync(no, _prefix, ct);
                order.OrderNo = no;
                order.Status = OrderStatus.Draft;
                order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
                order.CreatedAt = DateTime.Now;
                _db.PurchaseOrders.Add(order);
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
                var order = await _db.PurchaseOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id)
                    ?? throw new InvalidOperationException("采购单不存在");

                if (order.Status != OrderStatus.Draft)
                    throw new InvalidOperationException("仅草稿状态的采购单可以确认入库");

                if (order.Items.Count == 0)
                    throw new InvalidOperationException("采购单没有明细，无法入库");
                if (!order.SupplierId.HasValue)
                    throw new InvalidOperationException("采购单缺少供应商，无法入库");

                foreach (var item in order.Items)
                {
                    if (!item.ProductId.HasValue)
                        throw new InvalidOperationException("采购明细缺少商品");
                    var pid = item.ProductId.Value;
                    var prod = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == pid);
                    if (prod.TrackBatch)
                    {
                        var prodDate = (item.ProductionDate ?? DateTime.Today).Date;
                        var shelf = prod.ShelfLifeDays <= 0 ? 365 : prod.ShelfLifeDays;
                        var exp = prodDate.AddDays(shelf);
                        await _batch.ReceiveAsync(
                            pid,
                            item.Quantity,
                            item.UnitPrice,
                            item.BatchNo,
                            prodDate,
                            exp,
                            order.OrderNo,
                            item.Id,
                            StockChangeType.Purchase);
                    }
                    else
                    {
                        await _inventory.ApplyStockChangeAsync(
                            pid,
                            item.Quantity,
                            StockChangeType.Purchase,
                            order.OrderNo,
                            $"采购入库 #{order.OrderNo}");
                    }
                }

                order.Status = OrderStatus.Confirmed;
                order.ConfirmedAt = DateTime.Now;
                order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

                var supplier = await _db.Suppliers.AsNoTracking().FirstAsync(s => s.Id == order.SupplierId.Value);
                if (!await _db.Payables.AnyAsync(p => p.PurchaseOrderId == order.Id))
                {
                    _db.Payables.Add(new Payable
                    {
                        SupplierId = order.SupplierId.Value,
                        PurchaseOrderId = order.Id,
                        OrderNo = order.OrderNo,
                        Amount = order.TotalAmount,
                        Paid = 0,
                        DueDate = order.OrderDate.Date.AddDays(supplier.PaymentTermDays),
                        Status = PayableStatus.Outstanding,
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
                var order = await _db.PurchaseOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id)
                    ?? throw new InvalidOperationException("采购单不存在");

                if (order.Status != OrderStatus.Confirmed)
                    throw new InvalidOperationException("仅已确认入库的采购单可以退货");

                foreach (var item in order.Items)
                {
                    if (!item.ProductId.HasValue)
                        throw new InvalidOperationException("采购明细缺少商品");
                    var pid = item.ProductId.Value;
                    var prod = await _db.Products.AsNoTracking().FirstAsync(p => p.Id == pid);
                    if (prod.TrackBatch)
                    {
                        await _batch.ReturnPurchaseItemBatchesAsync(
                            item.Id,
                            item.Quantity,
                            order.OrderNo,
                            string.IsNullOrWhiteSpace(remark)
                                ? $"采购退货 #{order.OrderNo}"
                                : $"采购退货 #{order.OrderNo}：{remark}");
                    }
                    else
                    {
                        await _inventory.ApplyStockChangeAsync(
                            pid,
                            -item.Quantity,
                            StockChangeType.PurchaseReturn,
                            order.OrderNo,
                            string.IsNullOrWhiteSpace(remark)
                                ? $"采购退货 #{order.OrderNo}"
                                : $"采购退货 #{order.OrderNo}：{remark}");
                    }
                }

                var payable = await _db.Payables.FirstOrDefaultAsync(p => p.PurchaseOrderId == order.Id);
                if (payable != null)
                {
                    payable.Status = PayableStatus.WrittenOff;
                    payable.UpdatedAt = DateTime.Now;
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
            var order = await _db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new InvalidOperationException("采购单不存在");

            if (order.Status == OrderStatus.Confirmed)
                throw new InvalidOperationException("已确认入库的采购单不能取消，请使用退货或库存调整冲销");

            if (order.Status == OrderStatus.Returned)
                throw new InvalidOperationException("已退货的采购单无需取消");

            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var order = await _db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return;

            if (order.Status is OrderStatus.Confirmed or OrderStatus.Returned)
                throw new InvalidOperationException("已入库/已退货的采购单不可删除");

            _db.PurchaseOrders.Remove(order);
            await _db.SaveChangesAsync();
        }
    }
}
