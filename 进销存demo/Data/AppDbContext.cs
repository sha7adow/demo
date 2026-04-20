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
