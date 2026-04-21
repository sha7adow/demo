namespace 进销存demo.Models
{
    /// <summary>Excel 导入行（非 EF 实体）。</summary>
    public sealed class ProductImportRow
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "个";
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public int SafetyStock { get; set; }
        public bool TrackBatch { get; set; }
        public int ShelfLifeDays { get; set; } = 365;
        public string? CategoryName { get; set; }
        public string? Barcode { get; set; }
    }
}
