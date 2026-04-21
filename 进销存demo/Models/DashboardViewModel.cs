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

        /// <summary>30 天内到期且仍有剩余的批次数。</summary>
        public int ExpiringBatchCount30 { get; set; }

        /// <summary>逾期应收余额合计（到期日早于今天且未结清）。</summary>
        public decimal OverdueReceivableAmount { get; set; }

        /// <summary>未结清应收按客户汇总 TOP5（客户名, 未收余额）。</summary>
        public List<(string CustomerName, decimal Balance)> ReceivableTop5 { get; set; } = new();
    }
}
