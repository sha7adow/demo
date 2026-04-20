using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface IPurchaseService
    {
        Task<PurchaseOrder> CreateAsync(PurchaseOrder order);
        Task<PurchaseOrder?> GetAsync(int id);
        Task<List<PurchaseOrder>> ListAsync();
        Task ConfirmAsync(int id);
        Task CancelAsync(int id);
        Task DeleteAsync(int id);
        Task ReturnAsync(int id, string? remark = null);
        Task<string> GenerateOrderNoAsync();
    }
}
