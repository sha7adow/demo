using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface ISaleService
    {
        Task<SaleOrder> CreateAsync(SaleOrder order);
        Task<SaleOrder?> GetAsync(int id);
        Task<List<SaleOrder>> ListAsync();
        Task ConfirmAsync(int id);
        Task CancelAsync(int id);
        Task DeleteAsync(int id);
        Task ReturnAsync(int id, string? remark = null);
        Task<string> GenerateOrderNoAsync();
    }
}
