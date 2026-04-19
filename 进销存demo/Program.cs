using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// EF Core + SQLite
var connStr = builder.Configuration.GetConnectionString("Default")
              ?? "Data Source=jxc.db";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(connStr));

// 业务服务
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<ISaleService, SaleService>();

var app = builder.Build();

// 启动时初始化数据库（建库 + 种子数据）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
