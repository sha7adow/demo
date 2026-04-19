using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public class SaleService : ISaleService
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;

        public SaleService(AppDbContext db, IInventoryService inventory)
        {
            _db = db;
            _inventory = inventory;
        }

        public string GenerateOrderNo() =>
            $"SO{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}";

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
            if (string.IsNullOrWhiteSpace(order.OrderNo))
                order.OrderNo = GenerateOrderNo();

            order.Status = OrderStatus.Draft;
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            order.CreatedAt = DateTime.Now;

            _db.SaleOrders.Add(order);
            await _db.SaveChangesAsync();
            return order;
        }

        public async Task ConfirmAsync(int id)
        {
            var order = await _db.SaleOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new InvalidOperationException("销售单不存在");

            if (order.Status != OrderStatus.Draft)
                throw new InvalidOperationException("仅草稿状态的销售单可以确认出库");

            if (order.Items.Count == 0)
                throw new InvalidOperationException("销售单没有明细，无法出库");

            // 先做库存校验：把所有明细按商品聚合，确认整体库存够用，避免部分扣减后再失败
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

            // 出库：每条明细做一次负向库存变动
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
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            await _db.SaveChangesAsync();
        }

        public async Task CancelAsync(int id)
        {
            var order = await _db.SaleOrders.FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new InvalidOperationException("销售单不存在");

            if (order.Status == OrderStatus.Confirmed)
                throw new InvalidOperationException("已确认出库的销售单不能取消，请通过库存调整冲销");

            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var order = await _db.SaleOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return;

            if (order.Status == OrderStatus.Confirmed)
                throw new InvalidOperationException("已出库的销售单不可删除");

            _db.SaleOrders.Remove(order);
            await _db.SaveChangesAsync();
        }
    }
}
