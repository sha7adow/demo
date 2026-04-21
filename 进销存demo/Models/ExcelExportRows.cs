namespace 进销存demo.Models;

/// <summary>采购单明细导出（一行一条明细）。</summary>
public record PurchaseLineExcelRow(
    string OrderNo,
    DateTime OrderDate,
    string Status,
    string Supplier,
    string ProductCode,
    string ProductName,
    string Unit,
    int Quantity,
    decimal UnitPrice,
    decimal LineAmount,
    string? ProductionDate,
    string? BatchNo);

/// <summary>销售单明细导出（一行一条明细）。</summary>
public record SaleLineExcelRow(
    string OrderNo,
    DateTime OrderDate,
    string Status,
    string Customer,
    string ProductCode,
    string ProductName,
    string Unit,
    int Quantity,
    decimal UnitPrice,
    decimal LineAmount);
