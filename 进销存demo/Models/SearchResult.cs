using 进销存demo.Models.Entities;

namespace 进销存demo.Models;

public sealed class SearchResult
{
    public string Query { get; set; } = "";
    public IReadOnlyList<Product> Products { get; set; } = Array.Empty<Product>();
    public IReadOnlyList<PurchaseOrder> Purchases { get; set; } = Array.Empty<PurchaseOrder>();
    public IReadOnlyList<SaleOrder> Sales { get; set; } = Array.Empty<SaleOrder>();
}
