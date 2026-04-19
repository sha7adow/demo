using 进销存demo.Models.Entities;

namespace 进销存demo.Models
{
    public class DashboardViewModel
    {
        public int ProductCount { get; set; }
        public int SupplierCount { get; set; }
        public int CustomerCount { get; set; }

        public int LowStockCount { get; set; }
        public decimal StockValue { get; set; }

        public int TodayPurchaseCount { get; set; }
        public decimal TodayPurchaseAmount { get; set; }
        public int TodaySaleCount { get; set; }
        public decimal TodaySaleAmount { get; set; }

        public int DraftPurchaseCount { get; set; }
        public int DraftSaleCount { get; set; }

        public List<Product> LowStockTop { get; set; } = new();
        public List<StockTransaction> RecentTransactions { get; set; } = new();
    }
}
