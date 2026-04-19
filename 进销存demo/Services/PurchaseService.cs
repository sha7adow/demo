using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;

        public PurchaseService(AppDbContext db, IInventoryService inventory)
        {
            _db = db;
            _inventory = inventory;
        }

        public string GenerateOrderNo() =>
            $"PO{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}";

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
            if (string.IsNullOrWhiteSpace(order.OrderNo))
                order.OrderNo = GenerateOrderNo();

            order.Status = OrderStatus.Draft;
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            order.CreatedAt = DateTime.Now;

            _db.PurchaseOrders.Add(order);
            await _db.SaveChangesAsync();
            return order;
        }

        public async Task ConfirmAsync(int id)
        {
            var order = await _db.PurchaseOrders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new InvalidOperationException("采购单不存在");

            if (order.Status != OrderStatus.Draft)
                throw new InvalidOperationException("仅草稿状态的采购单可以确认入库");

            if (order.Items.Count == 0)
                throw new InvalidOperationException("采购单没有明细，无法入库");

            // 入库：每条明细做一次正向库存变动
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
            order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);
            await _db.SaveChangesAsync();
        }

        public async Task CancelAsync(int id)
        {
            var order = await _db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id)
                ?? throw new InvalidOperationException("采购单不存在");

            if (order.Status == OrderStatus.Confirmed)
                throw new InvalidOperationException("已确认入库的采购单不能取消，请通过库存调整冲销");

            order.Status = OrderStatus.Cancelled;
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var order = await _db.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null) return;

            if (order.Status == OrderStatus.Confirmed)
                throw new InvalidOperationException("已入库的采购单不可删除");

            _db.PurchaseOrders.Remove(order);
            await _db.SaveChangesAsync();
        }
    }
}
