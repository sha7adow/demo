using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Controllers.Api;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db) => _db = db;

    /// <summary>近 N 天已确认采购/销售金额（按日对齐，无数据为 0）。</summary>
    [HttpGet("trend")]
    public async Task<IActionResult> Trend([FromQuery] int days = 30)
    {
        days = Math.Clamp(days, 1, 366);
        var from = DateTime.Today.AddDays(-days + 1);

        var purRows = await _db.PurchaseOrders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Confirmed && o.OrderDate >= from)
            .Select(o => new { o.OrderDate, o.TotalAmount })
            .ToListAsync();
        var saleRows = await _db.SaleOrders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Confirmed && o.OrderDate >= from)
            .Select(o => new { o.OrderDate, o.TotalAmount })
            .ToListAsync();

        var purByDay = purRows.GroupBy(x => x.OrderDate.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));
        var saleByDay = saleRows.GroupBy(x => x.OrderDate.Date).ToDictionary(g => g.Key, g => g.Sum(x => x.TotalAmount));

        var dates = new List<string>();
        var purchase = new List<decimal>();
        var sale = new List<decimal>();
        for (var d = from.Date; d <= DateTime.Today.Date; d = d.AddDays(1))
        {
            dates.Add(d.ToString("yyyy-MM-dd"));
            purchase.Add(purByDay.TryGetValue(d, out var pa) ? pa : 0m);
            sale.Add(saleByDay.TryGetValue(d, out var sa) ? sa : 0m);
        }

        return Ok(new { dates, purchase, sale });
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> TopProducts([FromQuery] int take = 10)
    {
        take = Math.Clamp(take, 1, 50);
        var items = await _db.SaleOrderItems.AsNoTracking()
            .Include(i => i.SaleOrder)
            .Include(i => i.Product)
            .Where(i => i.SaleOrder != null && i.SaleOrder.Status == OrderStatus.Confirmed && i.ProductId != null)
            .ToListAsync();

        var top = items
            .GroupBy(i => i.ProductId!.Value)
            .Select(g => new
            {
                Code = g.Select(x => x.Product?.Code).FirstOrDefault() ?? "",
                Name = g.Select(x => x.Product?.Name).FirstOrDefault() ?? "",
                Qty = g.Sum(x => x.Quantity)
            })
            .OrderByDescending(x => x.Qty)
            .Take(take)
            .ToList();

        return Ok(new { items = top });
    }

    [HttpGet("stock-by-category")]
    public async Task<IActionResult> StockByCategory()
    {
        var rows = await _db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .Select(p => new { Cat = p.Category != null ? p.Category.Name : "未分类", p.Stock, p.PurchasePrice })
            .ToListAsync();

        var agg = rows
            .GroupBy(x => x.Cat)
            .Select(g => new { name = g.Key, value = g.Sum(x => x.Stock * x.PurchasePrice) })
            .OrderByDescending(x => x.value)
            .ToList();

        return Ok(new { categories = agg });
    }
}
