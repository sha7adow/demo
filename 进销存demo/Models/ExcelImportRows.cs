namespace 进销存demo.Models;

/// <summary>供应商 Excel 导入行。</summary>
public sealed class SupplierImportRow
{
    public string Name { get; set; } = "";
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public string? Remark { get; set; }
}

/// <summary>客户 Excel 导入行。</summary>
public sealed class CustomerImportRow
{
    public string Name { get; set; } = "";
    public string? Contact { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public int PaymentTermDays { get; set; } = 30;
    public string? Remark { get; set; }
}

/// <summary>采购单明细导入行（与导出表头一致；相同「单号」多行合并为一张草稿单）。</summary>
public sealed class PurchaseLineImportRow
{
    public string OrderNo { get; set; } = "";
    public string DateRaw { get; set; } = "";
    public string? StatusIgnored { get; set; }
    public string SupplierName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string? ProductNameIgnored { get; set; }
    public string? UnitIgnored { get; set; }
    public string QuantityRaw { get; set; } = "";
    public string UnitPriceRaw { get; set; } = "";
    public string? LineAmountIgnored { get; set; }
    public string? ProductionDateRaw { get; set; }
    public string? BatchNo { get; set; }
}

/// <summary>销售单明细导入行（与导出表头一致；相同「单号」多行合并为一张草稿单）。</summary>
public sealed class SaleLineImportRow
{
    public string OrderNo { get; set; } = "";
    public string DateRaw { get; set; } = "";
    public string? StatusIgnored { get; set; }
    public string CustomerName { get; set; } = "";
    public string ProductCode { get; set; } = "";
    public string? ProductNameIgnored { get; set; }
    public string? UnitIgnored { get; set; }
    public string QuantityRaw { get; set; } = "";
    public string UnitPriceRaw { get; set; } = "";
    public string? LineAmountIgnored { get; set; }
}
