using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;

namespace 进销存demo.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
        public DbSet<SaleOrder> SaleOrders => Set<SaleOrder>();
        public DbSet<SaleOrderItem> SaleOrderItems => Set<SaleOrderItem>();
        public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();
        public DbSet<SequenceNumber> SequenceNumbers => Set<SequenceNumber>();
        public DbSet<Stocktake> Stocktakes => Set<Stocktake>();
        public DbSet<StocktakeItem> StocktakeItems => Set<StocktakeItem>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
        public DbSet<Receivable> Receivables => Set<Receivable>();
        public DbSet<PaymentReceipt> PaymentReceipts => Set<PaymentReceipt>();
        public DbSet<Payable> Payables => Set<Payable>();
        public DbSet<PaymentVoucher> PaymentVouchers => Set<PaymentVoucher>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b); // Identity 的表结构

            // ---------- 唯一索引（软删除后允许同名重用：加 Filter）----------
            b.Entity<Product>()
                .HasIndex(x => x.Code)
                .IsUnique()
                .HasFilter("\"IsDeleted\" = 0");

            b.Entity<PurchaseOrder>().HasIndex(x => x.OrderNo).IsUnique();
            b.Entity<SaleOrder>().HasIndex(x => x.OrderNo).IsUnique();
            b.Entity<Stocktake>().HasIndex(x => x.OrderNo).IsUnique();

            b.Entity<SequenceNumber>()
                .HasIndex(x => x.Scope)
                .IsUnique();

            b.Entity<StockTransaction>()
                .HasIndex(x => x.RefOrderNo);
            b.Entity<StockTransaction>()
                .HasIndex(x => new { x.ProductId, x.Id });

            b.Entity<AuditLog>()
                .HasIndex(x => new { x.EntityType, x.EntityKey });
            b.Entity<AuditLog>()
                .HasIndex(x => x.CreatedAt);

            b.Entity<ProductBatch>()
                .HasIndex(x => new { x.ProductId, x.BatchNo })
                .IsUnique();
            b.Entity<ProductBatch>()
                .HasOne(x => x.Product)
                .WithMany()
                .HasForeignKey(x => x.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            b.Entity<ProductBatch>()
                .HasOne(x => x.PurchaseOrderItem)
                .WithMany()
                .HasForeignKey(x => x.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Entity<StockTransaction>()
                .HasOne(x => x.Batch)
                .WithMany()
                .HasForeignKey(x => x.BatchId)
                .OnDelete(DeleteBehavior.SetNull);

            b.Entity<Receivable>()
                .HasIndex(x => x.SaleOrderId)
                .IsUnique();
            b.Entity<Receivable>()
                .HasOne(r => r.SaleOrder)
                .WithOne(s => s.Receivable)
                .HasForeignKey<Receivable>(r => r.SaleOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Entity<Receivable>()
                .HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            b.Entity<PaymentReceipt>()
                .HasOne(x => x.Receivable)
                .WithMany(r => r.Receipts)
                .HasForeignKey(x => x.ReceivableId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<Payable>()
                .HasIndex(x => x.PurchaseOrderId)
                .IsUnique();
            b.Entity<Payable>()
                .HasOne(p => p.PurchaseOrder)
                .WithOne(o => o.Payable)
                .HasForeignKey<Payable>(p => p.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Entity<Payable>()
                .HasOne(p => p.Supplier)
                .WithMany()
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            b.Entity<PaymentVoucher>()
                .HasOne(x => x.Payable)
                .WithMany(p => p.Vouchers)
                .HasForeignKey(x => x.PayableId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- decimal 精度 ----------
            foreach (var prop in b.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                prop.SetPrecision(18);
                prop.SetScale(2);
            }

            // ---------- 关系 ----------
            b.Entity<PurchaseOrderItem>()
                .HasOne(x => x.PurchaseOrder)
                .WithMany(o => o.Items)
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<SaleOrderItem>()
                .HasOne(x => x.SaleOrder)
                .WithMany(o => o.Items)
                .HasForeignKey(x => x.SaleOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<StocktakeItem>()
                .HasOne(x => x.Stocktake)
                .WithMany(s => s.Items)
                .HasForeignKey(x => x.StocktakeId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany()
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            // 软删主体被全局过滤时，依赖外键的导航改为可选，消除 Model.Validation[10622] 警告
            b.Entity<PurchaseOrder>()
                .HasOne(o => o.Supplier)
                .WithMany()
                .HasForeignKey(o => o.SupplierId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            b.Entity<SaleOrder>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            b.Entity<PurchaseOrderItem>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            b.Entity<SaleOrderItem>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            b.Entity<StockTransaction>()
                .HasOne(t => t.Product)
                .WithMany()
                .HasForeignKey(t => t.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
            b.Entity<StocktakeItem>()
                .HasOne(i => i.Product)
                .WithMany()
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // ---------- 乐观锁 ----------
            b.Entity<Product>()
                .Property(p => p.RowVersion)
                .IsConcurrencyToken();

            // ---------- 软删除全局过滤 ----------
            b.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
            b.Entity<ProductCategory>().HasQueryFilter(c => !c.IsDeleted);
            b.Entity<Supplier>().HasQueryFilter(s => !s.IsDeleted);
            b.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);

            // 计算属性不落库
            b.Entity<StocktakeItem>().Ignore(i => i.Diff);
            b.Entity<PurchaseOrderItem>().Ignore(i => i.Subtotal);
            b.Entity<SaleOrderItem>().Ignore(i => i.Subtotal);
        }
    }
}
