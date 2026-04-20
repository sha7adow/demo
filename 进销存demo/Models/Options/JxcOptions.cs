namespace 进销存demo.Models.Options
{
    /// <summary>
    /// 全局业务可调参数，对应 appsettings.json 中的 "Jxc" 节点。
    /// </summary>
    public class JxcOptions
    {
        public const string SectionName = "Jxc";

        public OrderPrefixOptions OrderPrefix { get; set; } = new();
        public PagingOptions Paging { get; set; } = new();
        public InventoryOptions Inventory { get; set; } = new();
        public SeedOptions Seed { get; set; } = new();
    }

    public class OrderPrefixOptions
    {
        public string Purchase { get; set; } = "PO";
        public string Sale { get; set; } = "SO";
        public string Stocktake { get; set; } = "ST";
    }

    public class PagingOptions
    {
        public int DefaultPageSize { get; set; } = 20;
        public int MaxPageSize { get; set; } = 100;
    }

    public class InventoryOptions
    {
        /// <summary>
        /// 低库存预警阈值倍数。stock &lt;= SafetyStock * Factor 即进入预警。
        /// 默认 1.0 即 stock &lt;= SafetyStock。
        /// </summary>
        public double WarningFactor { get; set; } = 1.0;
    }

    public class SeedOptions
    {
        /// <summary>
        /// 默认账户密码。仅在首次种子时生效；生产请改到 UserSecrets/环境变量。
        /// </summary>
        public string DefaultPassword { get; set; } = "Jxc@123456";
    }
}
