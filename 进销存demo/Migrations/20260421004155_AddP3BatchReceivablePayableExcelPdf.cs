using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace 进销存demo.Migrations
{
    /// <inheritdoc />
    public partial class AddP3BatchReceivablePayableExcelPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PaymentTermDays",
                table: "Suppliers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BatchId",
                table: "StockTransactions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsumedFromBatches",
                table: "SaleOrderItems",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "PurchaseOrderItems",
                type: "TEXT",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProductionDate",
                table: "PurchaseOrderItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeDays",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "TrackBatch",
                table: "Products",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PaymentTermDays",
                table: "Customers",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Payables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SupplierId = table.Column<int>(type: "INTEGER", nullable: false),
                    PurchaseOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderNo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Paid = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payables_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payables_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BatchNo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ProductId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProductionDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InitialQty = table.Column<int>(type: "INTEGER", nullable: false),
                    RemainingQty = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitCost = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PurchaseOrderItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductBatches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductBatches_PurchaseOrderItems_PurchaseOrderItemId",
                        column: x => x.PurchaseOrderItemId,
                        principalTable: "PurchaseOrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Receivables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: false),
                    SaleOrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderNo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Paid = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    DueDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receivables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receivables_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receivables_SaleOrders_SaleOrderId",
                        column: x => x.SaleOrderId,
                        principalTable: "SaleOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentVouchers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderNo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    PayableId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaidDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentVouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentVouchers_Payables_PayableId",
                        column: x => x.PayableId,
                        principalTable: "Payables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentReceipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderNo = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ReceivableId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    PaidDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Remark = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReceipts_Receivables_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "Receivables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockTransactions_BatchId",
                table: "StockTransactions",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Payables_PurchaseOrderId",
                table: "Payables",
                column: "PurchaseOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payables_SupplierId",
                table: "Payables",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_ReceivableId",
                table: "PaymentReceipts",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentVouchers_PayableId",
                table: "PaymentVouchers",
                column: "PayableId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_ProductId_BatchNo",
                table: "ProductBatches",
                columns: new[] { "ProductId", "BatchNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductBatches_PurchaseOrderItemId",
                table: "ProductBatches",
                column: "PurchaseOrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Receivables_CustomerId",
                table: "Receivables",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Receivables_SaleOrderId",
                table: "Receivables",
                column: "SaleOrderId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTransactions_ProductBatches_BatchId",
                table: "StockTransactions",
                column: "BatchId",
                principalTable: "ProductBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockTransactions_ProductBatches_BatchId",
                table: "StockTransactions");

            migrationBuilder.DropTable(
                name: "PaymentReceipts");

            migrationBuilder.DropTable(
                name: "PaymentVouchers");

            migrationBuilder.DropTable(
                name: "ProductBatches");

            migrationBuilder.DropTable(
                name: "Receivables");

            migrationBuilder.DropTable(
                name: "Payables");

            migrationBuilder.DropIndex(
                name: "IX_StockTransactions_BatchId",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentTermDays",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "StockTransactions");

            migrationBuilder.DropColumn(
                name: "ConsumedFromBatches",
                table: "SaleOrderItems");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ProductionDate",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "ShelfLifeDays",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrackBatch",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PaymentTermDays",
                table: "Customers");
        }
    }
}
