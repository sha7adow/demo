using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly AppDbContext _db;
        public InventoryService(AppDbContext db) { _db = db; }

        public async Task ApplyStockChangeAsync(int productId, int quantityDelta, StockChangeType type, string? refNo, string? remark = null)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId)
                ?? throw new InvalidOperationException($"商品不存在：{productId}");

            var newStock = product.Stock + quantityDelta;
            if (newStock < 0)
                throw new InvalidOperationException($"库存不足：{product.Name} 当前 {product.Stock}，本次需出 {-quantityDelta}");

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

            await _db.SaveChangesAsync();
        }
    }
}
