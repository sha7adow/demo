using 进销存demo.Models.Entities;

namespace 进销存demo.Models.Queries
{
    public class ProductQuery
    {
        public string? Keyword { get; set; }
        public int? CategoryId { get; set; }
        public bool? OnlyWarning { get; set; }
        public bool? OnlyActive { get; set; } = true;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SupplierQuery
    {
        public string? Keyword { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class CustomerQuery
    {
        public string? Keyword { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class PurchaseQuery
    {
        public string? Keyword { get; set; }         // 单号
        public int? SupplierId { get; set; }
        public OrderStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class SaleQuery
    {
        public string? Keyword { get; set; }
        public int? CustomerId { get; set; }
        public OrderStatus? Status { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class TransactionQuery
    {
        public int? ProductId { get; set; }
        public StockChangeType? ChangeType { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }
}
