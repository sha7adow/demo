using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface IInventoryService
    {
        Task ApplyStockChangeAsync(int productId, int quantityDelta, StockChangeType type, string? refNo, string? remark = null);
    }
}
