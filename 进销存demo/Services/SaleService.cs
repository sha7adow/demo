using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 进销存demo.Data;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Options;

namespace 进销存demo.Services
{
    public class SaleService : ISaleService
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IOrderNoGenerator _orderNo;
        private readonly string _prefix;

        public SaleService(AppDbContext db, IInventoryService inventory, IOrderNoGenerator orderNo, IOptions<JxcOptions> options)
        {
            _db = db;
            _inventory = inventory;
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
            order.OrderNo = await _orderNo.NextAsync(_prefix);
            order.Status = OrderStatus.Draft;
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            order.CreatedAt = DateTime.Now;

            _db.SaleOrders.Add(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return order;
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

                // 聚合校验：同商品多明细汇总后再判断库存
                var grouped = order.Items
                    .GroupBy(i => i.ProductId)
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
                    if (p.Stock < g.Qty)
                        throw new InvalidOperationException($"库存不足：{p.Name} 当前 {p.Stock}，需出 {g.Qty}");
                }

                foreach (var item in order.Items)
                {
                    await _inventory.ApplyStockChangeAsync(
                        item.ProductId,
                        -item.Quantity,
                        StockChangeType.Sale,
                        order.OrderNo,
                        $"销售出库 #{order.OrderNo}");
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
                var order = await _db.SaleOrders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id)
                    ?? throw new InvalidOperationException("销售单不存在");

                if (order.Status != OrderStatus.Confirmed)
                    throw new InvalidOperationException("仅已确认出库的销售单可以退货");

                foreach (var item in order.Items)
                {
                    await _inventory.ApplyStockChangeAsync(
                        item.ProductId,
                        item.Quantity,
                        StockChangeType.SaleReturn,
                        order.OrderNo,
                        string.IsNullOrWhiteSpace(remark)
                            ? $"销售退货 #{order.OrderNo}"
                            : $"销售退货 #{order.OrderNo}：{remark}");
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
