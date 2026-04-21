using 进销存demo.Models.Entities;

namespace 进销存demo.Models.Queries
{
    public class ProductQuery : IPagedQuery
    {
        public string? Keyword { get; set; }
        public int? CategoryId { get; set; }
        public bool? OnlyWarning { get; set; }
        public bool? OnlyActive { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; }
    }

    public class SupplierQuery : IPagedQuery
    {
        public string? Keyword { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; }
    }

    public class CustomerQuery : IPagedQuery
    {
        public string? Keyword { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; }
    }

    public class PurchaseQuery : IPagedQuery
    {
        public string? Keyword { get; set; }         // 单号
        public int? SupplierId { get; set; }
        public OrderStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; }
    }

    public class SaleQuery : IPagedQuery
    {
        public string? Keyword { get; set; }
        public int? CustomerId { get; set; }
        public OrderStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; }
    }

    public class TransactionQuery : IPagedQuery
    {
        public int? ProductId { get; set; }
        public StockChangeType? ChangeType { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; }
    }
}
