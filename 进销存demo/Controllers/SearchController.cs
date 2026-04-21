using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;

namespace 进销存demo.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly AppDbContext _db;

    public SearchController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
            return View(new SearchResult { Query = q ?? "" });

        var key = q.Trim();
        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.Code.Contains(key) || p.Name.Contains(key) || (p.Barcode != null && p.Barcode.Contains(key)))
            .OrderBy(p => p.Code)
            .Take(10)
            .ToListAsync();

        var canPurchase = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Purchaser);
        var purchases = canPurchase
            ? await _db.PurchaseOrders
                .Include(o => o.Supplier)
                .Where(o => o.OrderNo.Contains(key))
                .OrderByDescending(o => o.Id)
                .Take(10)
                .ToListAsync()
            : new List<PurchaseOrder>();

        var canSale = User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Salesperson);
        var sales = canSale
            ? await _db.SaleOrders
                .Include(o => o.Customer)
                .Where(o => o.OrderNo.Contains(key))
                .OrderByDescending(o => o.Id)
                .Take(10)
                .ToListAsync()
            : new List<SaleOrder>();

        return View(new SearchResult
        {
            Query = key,
            Products = products,
            Purchases = purchases,
            Sales = sales
        });
    }
}
