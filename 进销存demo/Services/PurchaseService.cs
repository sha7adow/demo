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
        private readonly IOrderNoGenerator _orderNo;
        private readonly string _prefix;

        public PurchaseService(AppDbContext db, IInventoryService inventory, IOrderNoGenerator orderNo, IOptions<JxcOptions> options)
        {
            _db = db;
            _inventory = inventory;
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
            order.OrderNo = await _orderNo.NextAsync(_prefix);
            order.Status = OrderStatus.Draft;
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            order.CreatedAt = DateTime.Now;

            _db.PurchaseOrders.Add(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return order;
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

                foreach (var item in order.Items)
                {
                    await _inventory.ApplyStockChangeAsync(
                        item.ProductId,
                        item.Quantity,
                        StockChangeType.Purchase,
                        order.OrderNo,
                        $"采购入库 #{order.OrderNo}");
                }

                order.Status = OrderStatus.Confirmed;
                order.ConfirmedAt = DateTime.Now;
                order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

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
                    await _inventory.ApplyStockChangeAsync(
                        item.ProductId,
                        -item.Quantity,
                        StockChangeType.PurchaseReturn,
                        order.OrderNo,
                        string.IsNullOrWhiteSpace(remark)
                            ? $"采购退货 #{order.OrderNo}"
                            : $"采购退货 #{order.OrderNo}：{remark}");
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
