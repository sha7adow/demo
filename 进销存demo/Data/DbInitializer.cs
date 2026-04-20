using Microsoft.AspNetCore.Identity;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;

namespace 进销存demo.Data
{
    public static class DbInitializer
    {
        /// <summary>
        /// 业务主数据种子（分类、商品、供应商、客户）
        /// </summary>
        public static void Seed(AppDbContext db)
        {
            if (!db.ProductCategories.Any())
            {
                db.ProductCategories.AddRange(
                    new ProductCategory { Name = "饮料" },
                    new ProductCategory { Name = "方便食品" },
                    new ProductCategory { Name = "休闲零食" }
                );
                db.SaveChanges();
            }

            if (!db.Products.Any())
            {
                var drink = db.ProductCategories.First(c => c.Name == "饮料").Id;
                var noodle = db.ProductCategories.First(c => c.Name == "方便食品").Id;
                var snack = db.ProductCategories.First(c => c.Name == "休闲零食").Id;

                db.Products.AddRange(
                    new Product { Code = "P001", Name = "可口可乐 330ml", Unit = "瓶", Barcode = "6901234500011", CategoryId = drink,  PurchasePrice = 2.0m, SalePrice = 3.5m, Stock = 100, SafetyStock = 20 },
                    new Product { Code = "P002", Name = "农夫山泉 550ml", Unit = "瓶", Barcode = "6901234500028", CategoryId = drink,  PurchasePrice = 1.2m, SalePrice = 2.0m, Stock = 200, SafetyStock = 30 },
                    new Product { Code = "P003", Name = "康师傅红烧牛肉面", Unit = "桶", Barcode = "6901234500035", CategoryId = noodle, PurchasePrice = 3.5m, SalePrice = 5.5m, Stock = 80,  SafetyStock = 15 },
                    new Product { Code = "P004", Name = "乐事薯片 75g",     Unit = "袋", Barcode = "6901234500042", CategoryId = snack,  PurchasePrice = 4.0m, SalePrice = 6.5m, Stock = 60,  SafetyStock = 10 }
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

        /// <summary>
        /// Identity 种子：创建 4 个角色 + 默认用户（admin / purchaser / salesperson / warehouse）。
        /// 默认密码都是 <c>Jxc@123456</c>，首次登录后请立即在后台改密。
        /// </summary>
        public static async Task SeedIdentityAsync(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager,
            string defaultPassword = "Jxc@123456")
        {
            foreach (var role in Roles.All)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // (用户名, 邮箱, 显示名, 角色)
            var defaults = new (string u, string e, string name, string role)[]
            {
                ("admin",       "admin@jxc.local",       "系统管理员", Roles.Admin),
                ("purchaser",   "purchaser@jxc.local",   "采购员甲",   Roles.Purchaser),
                ("salesperson", "salesperson@jxc.local", "销售员甲",   Roles.Salesperson),
                ("warehouse",   "warehouse@jxc.local",   "库管甲",     Roles.Warehouse),
            };

            foreach (var (u, e, name, role) in defaults)
            {
                var exist = await userManager.FindByNameAsync(u);
                if (exist == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = u,
                        Email = e,
                        EmailConfirmed = true,
                        DisplayName = name,
                        IsEnabled = true
                    };
                    var r = await userManager.CreateAsync(user, defaultPassword);
                    if (r.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, role);
                    }
                }
            }
        }
    }
}
