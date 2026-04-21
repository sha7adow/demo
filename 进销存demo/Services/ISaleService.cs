using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface ISaleService
    {
        Task<SaleOrder> CreateAsync(SaleOrder order);

        /// <summary>批量导入草稿销售单（同一事务；单号须已在外部校验唯一且与导出格式一致）。</summary>
        Task<int> CreateManyFromImportAsync(IReadOnlyList<SaleOrder> orders, CancellationToken ct = default);
        Task<SaleOrder?> GetAsync(int id);
        Task<List<SaleOrder>> ListAsync();
        Task ConfirmAsync(int id);
        Task CancelAsync(int id);
        Task DeleteAsync(int id);
        Task ReturnAsync(int id, string? remark = null);
        Task<string> GenerateOrderNoAsync();
    }
}
