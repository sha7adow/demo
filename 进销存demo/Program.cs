using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using 进销存demo.Data;
using 进销存demo.Filters;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;
using 进销存demo.Models.Options;
using 进销存demo.Services;
using QuestPDF.Infrastructure;

// ---------- Serilog：启动前最早可用 ----------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/jxc-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("=========== 进销存demo 启动 ===========");

    var builder = WebApplication.CreateBuilder(args);

    QuestPDF.Settings.License = LicenseType.Community;

    // 用 Serilog 替换默认日志提供者，同时读取 appsettings.json 的 Serilog 节点
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "Logs/jxc-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"));

    // ---------- Options ----------
    builder.Services.Configure<JxcOptions>(builder.Configuration.GetSection(JxcOptions.SectionName));
    builder.Services.Configure<FormOptions>(o =>
    {
        o.MultipartBodyLengthLimit = 2_000_000;
    });

    var dpPath = Path.Combine(builder.Environment.ContentRootPath, "data", "dp-keys");
    Directory.CreateDirectory(dpPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dpPath))
        .SetApplicationName("JxcDemo");

    // ---------- MVC ----------
    builder.Services.AddScoped<PopulatePagingDefaultsFilter>();
    builder.Services.AddControllersWithViews(o =>
        o.Filters.AddService<PopulatePagingDefaultsFilter>());
    builder.Services.AddHttpContextAccessor();

    // ---------- EF Core + SQLite ----------
    var connStr = builder.Configuration.GetConnectionString("Default")
                  ?? "Data Source=jxc.db";

    builder.Services.AddSingleton<AuditSaveChangesInterceptor>();

    builder.Services.AddDbContext<AppDbContext>((sp, opt) =>
    {
        opt.UseSqlite(connStr);
        opt.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
    });

    // ---------- Identity ----------
    builder.Services
        .AddIdentity<ApplicationUser, IdentityRole>(opt =>
        {
            // 密码策略（Demo 宽松些；生产环境可调严）
            opt.Password.RequireDigit = true;
            opt.Password.RequireLowercase = false;
            opt.Password.RequireUppercase = false;
            opt.Password.RequireNonAlphanumeric = false;
            opt.Password.RequiredLength = 6;

            // 登录失败锁定
            opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            opt.Lockout.MaxFailedAccessAttempts = 5;

            opt.User.RequireUniqueEmail = false;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

    builder.Services.ConfigureApplicationCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.LogoutPath = "/Account/Logout";
        opt.AccessDeniedPath = "/Account/AccessDenied";
        opt.ExpireTimeSpan = TimeSpan.FromHours(8);
        opt.SlidingExpiration = true;
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Lax;
    });

    // ---------- 业务服务 ----------
    builder.Services.AddScoped<IBatchInventoryService, BatchInventoryService>();
    builder.Services.AddScoped<IInventoryService, InventoryService>();
    builder.Services.AddScoped<IExcelService, ExcelService>();
    builder.Services.AddScoped<IPdfService, PdfService>();
    builder.Services.AddScoped<IOrderNoGenerator, OrderNoGenerator>();
    builder.Services.AddScoped<IPurchaseService, PurchaseService>();
    builder.Services.AddScoped<ISaleService, SaleService>();
    builder.Services.AddScoped<IStocktakeService, StocktakeService>();

    var app = builder.Build();

    // 静态导入模板（首次启动写入 wwwroot/templates/products-template.xlsx）
    try
    {
        using var tplScope = app.Services.CreateScope();
        var env = tplScope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
        var excel = tplScope.ServiceProvider.GetRequiredService<IExcelService>();
        var tplDir = Path.Combine(env.WebRootPath, "templates");
        Directory.CreateDirectory(tplDir);
        var tplPath = Path.Combine(tplDir, "products-template.xlsx");
        if (!File.Exists(tplPath))
        {
            var cols = new[]
            {
                new ColumnSpec<Product>("编码", x => x.Code),
                new ColumnSpec<Product>("名称", x => x.Name),
                new ColumnSpec<Product>("单位", x => x.Unit),
                new ColumnSpec<Product>("采购价", x => x.PurchasePrice),
                new ColumnSpec<Product>("销售价", x => x.SalePrice),
                new ColumnSpec<Product>("安全库存", x => x.SafetyStock),
                new ColumnSpec<Product>("启用批次", x => x.TrackBatch ? 1 : 0),
                new ColumnSpec<Product>("保质期天", x => x.ShelfLifeDays),
                new ColumnSpec<Product>("分类", x => x.Category?.Name),
                new ColumnSpec<Product>("条码", x => x.Barcode),
            };
            await File.WriteAllBytesAsync(tplPath, excel.Export(Array.Empty<Product>(), "商品", cols));
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "写入商品导入模板失败，可继续使用「下载导入模板」链接");
    }

    // ---------- 启动：迁移 + 种子 ----------
    using (var scope = app.Services.CreateScope())
    {
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        DbInitializer.Seed(db);

        var roleMgr = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userMgr = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var seedPwd = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<JxcOptions>>().Value.Seed.DefaultPassword;
        await DbInitializer.SeedIdentityAsync(roleMgr, userMgr, seedPwd);
    }

    // ---------- 统一异常处理 ----------
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseSerilogRequestLogging();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "主机启动失败");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
