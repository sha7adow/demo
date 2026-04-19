using Microsoft.EntityFrameworkCore;
using 进销存demo.Models.Entities;

namespace 进销存demo.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
        public DbSet<SaleOrder> SaleOrders => Set<SaleOrder>();
        public DbSet<SaleOrderItem> SaleOrderItems => Set<SaleOrderItem>();
        public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<Product>().HasIndex(x => x.Code).IsUnique();
            b.Entity<PurchaseOrder>().HasIndex(x => x.OrderNo).IsUnique();
            b.Entity<SaleOrder>().HasIndex(x => x.OrderNo).IsUnique();

            // SQLite 下保留两位小数即可，使用 TEXT 存储 decimal 由 EF 自动处理
            foreach (var prop in b.Model.GetEntityTypes()
                         .SelectMany(t => t.GetProperties())
                         .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                prop.SetPrecision(18);
                prop.SetScale(2);
            }

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
        }
    }

    public static class DbInitializer
    {
        public static void Seed(AppDbContext db)
        {
            db.Database.EnsureCreated();

            if (!db.Products.Any())
            {
                db.Products.AddRange(
                    new Product { Code = "P001", Name = "可口可乐 330ml", Unit = "瓶", PurchasePrice = 2.0m, SalePrice = 3.5m, Stock = 100, SafetyStock = 20 },
                    new Product { Code = "P002", Name = "农夫山泉 550ml", Unit = "瓶", PurchasePrice = 1.2m, SalePrice = 2.0m, Stock = 200, SafetyStock = 30 },
                    new Product { Code = "P003", Name = "康师傅红烧牛肉面", Unit = "桶", PurchasePrice = 3.5m, SalePrice = 5.5m, Stock = 80,  SafetyStock = 15 },
                    new Product { Code = "P004", Name = "乐事薯片 75g",     Unit = "袋", PurchasePrice = 4.0m, SalePrice = 6.5m, Stock = 60,  SafetyStock = 10 }
                );
            }

            if (!db.Suppliers.Any())
            {
                db.Suppliers.AddRange(
                    new Supplier { Name = "可口可乐华南分公司", Contact = "李经理", Phone = "13800001111", Address = "广州市" },
                    new Supplier { Name = "农夫山泉股份有限公司", Contact = "王经理", Phone = "13800002222", Address = "杭州市" },
                    new Supplier { Name = "康师傅食品",         Contact = "赵经理", Phone = "13800003333", Address = "天津市" }
                );
            }

            if (!db.Customers.Any())
            {
                db.Customers.AddRange(
                    new Customer { Name = "便利店 A", Contact = "张老板", Phone = "13900001111", Address = "广州天河" },
                    new Customer { Name = "便利店 B", Contact = "刘老板", Phone = "13900002222", Address = "广州海珠" },
                    new Customer { Name = "超市 C",   Contact = "陈经理", Phone = "13900003333", Address = "广州番禺" }
                );
            }

            db.SaveChanges();
        }
    }
}
