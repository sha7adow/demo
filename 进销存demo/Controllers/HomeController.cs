using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models;
using 进销存demo.Models.Entities;

namespace 进销存demo.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        public HomeController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var vm = new DashboardViewModel
            {
                ProductCount = await _db.Products.CountAsync(),
                SupplierCount = await _db.Suppliers.CountAsync(),
                CustomerCount = await _db.Customers.CountAsync(),

                LowStockCount = await _db.Products.CountAsync(p => p.Stock <= p.SafetyStock),
                // SQLite 不支持服务端 SUM(decimal)，改为客户端聚合
                StockValue = (await _db.Products
                                  .Select(p => new { p.Stock, p.PurchasePrice })
                                  .ToListAsync())
                              .Sum(x => x.Stock * x.PurchasePrice),

                TodayPurchaseCount = await _db.PurchaseOrders
                    .CountAsync(o => o.Status == OrderStatus.Confirmed && o.OrderDate >= today && o.OrderDate < tomorrow),
                TodayPurchaseAmount = (await _db.PurchaseOrders
                    .Where(o => o.Status == OrderStatus.Confirmed && o.OrderDate >= today && o.OrderDate < tomorrow)
                    .Select(o => o.TotalAmount).ToListAsync()).Sum(),

                TodaySaleCount = await _db.SaleOrders
                    .CountAsync(o => o.Status == OrderStatus.Confirmed && o.OrderDate >= today && o.OrderDate < tomorrow),
                TodaySaleAmount = (await _db.SaleOrders
                    .Where(o => o.Status == OrderStatus.Confirmed && o.OrderDate >= today && o.OrderDate < tomorrow)
                    .Select(o => o.TotalAmount).ToListAsync()).Sum(),

                DraftPurchaseCount = await _db.PurchaseOrders.CountAsync(o => o.Status == OrderStatus.Draft),
                DraftSaleCount = await _db.SaleOrders.CountAsync(o => o.Status == OrderStatus.Draft),

                LowStockTop = await _db.Products
                    .Where(p => p.Stock <= p.SafetyStock)
                    .OrderBy(p => p.Stock)
                    .Take(10)
                    .ToListAsync(),

                RecentTransactions = await _db.StockTransactions
                    .Include(t => t.Product)
                    .OrderByDescending(t => t.Id)
                    .Take(10)
                    .ToListAsync()
            };

            var end30 = today.AddDays(30);
            vm.ExpiringBatchCount30 = await _db.ProductBatches.CountAsync(b =>
                b.RemainingQty > 0 && b.ExpiryDate.Date > today && b.ExpiryDate.Date <= end30);

            var outstandingRecv = await _db.Receivables
                .Include(r => r.Customer)
                .Where(r => r.Status == ReceivableStatus.Outstanding)
                .ToListAsync();
            vm.OverdueReceivableAmount = outstandingRecv
                .Where(r => r.DueDate.Date < today)
                .Sum(r => r.Amount - r.Paid);
            vm.ReceivableTop5 = outstandingRecv
                .GroupBy(r => r.Customer!.Name)
                .Select(g => (g.Key, g.Sum(x => x.Amount - x.Paid)))
                .OrderByDescending(x => x.Item2)
                .Take(5)
                .ToList();

            return View(vm);
        }

        [AllowAnonymous]
        public IActionResult Privacy() => View();

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusCode(int? code)
        {
            if (code == 403)
                return RedirectToAction("AccessDenied", "Account");
            ViewBag.Code = code ?? 0;
            return View("StatusCode");
        }
    }
}
