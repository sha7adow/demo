using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface IPurchaseService
    {
        Task<PurchaseOrder> CreateAsync(PurchaseOrder order);

        /// <summary>批量导入草稿采购单（同一事务；单号须已在外部校验唯一且与导出格式一致）。</summary>
        Task<int> CreateManyFromImportAsync(IReadOnlyList<PurchaseOrder> orders, CancellationToken ct = default);
        Task<PurchaseOrder?> GetAsync(int id);
        Task<List<PurchaseOrder>> ListAsync();
        Task ConfirmAsync(int id);
        Task CancelAsync(int id);
        Task DeleteAsync(int id);
        Task ReturnAsync(int id, string? remark = null);
        Task<string> GenerateOrderNoAsync();
    }
}
