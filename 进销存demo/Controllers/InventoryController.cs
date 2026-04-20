using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;
using 进销存demo.Models.Queries;
using 进销存demo.Services;

namespace 进销存demo.Controllers
{
    [Authorize]
    public class InventoryController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;

        public InventoryController(AppDbContext db, IInventoryService inventory)
        {
            _db = db;
            _inventory = inventory;
        }

        public async Task<IActionResult> Index([FromQuery] ProductQuery q)
        {
            var query = _db.Products.Include(p => p.Category).Where(p => p.IsActive).AsQueryable();

            if (!string.IsNullOrWhiteSpace(q.Keyword))
                query = query.Where(p => p.Code.Contains(q.Keyword) || p.Name.Contains(q.Keyword));

            if (q.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == q.CategoryId);

            if (q.OnlyWarning == true)
                query = query.Where(p => p.Stock <= p.SafetyStock);

            var paged = await PagedList<Product>.CreateAsync(query.OrderBy(p => p.Code), q.Page, q.PageSize);

            ViewBag.Query = q;
            ViewBag.Categories = await _db.ProductCategories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Page = paged.PageIndex;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            return View(paged.Items);
        }

        public async Task<IActionResult> Transactions([FromQuery] TransactionQuery q)
        {
            var query = _db.StockTransactions.Include(t => t.Product).AsQueryable();

            if (q.ProductId.HasValue)
                query = query.Where(t => t.ProductId == q.ProductId.Value);
            if (q.ChangeType.HasValue)
                query = query.Where(t => t.ChangeType == q.ChangeType.Value);
            if (q.DateFrom.HasValue)
                query = query.Where(t => t.CreatedAt >= q.DateFrom.Value.Date);
            if (q.DateTo.HasValue)
            {
                var end = q.DateTo.Value.Date.AddDays(1);
                query = query.Where(t => t.CreatedAt < end);
            }

            var paged = await PagedList<StockTransaction>.CreateAsync(
                query.OrderByDescending(t => t.Id), q.Page, q.PageSize);

            ViewBag.Query = q;
            ViewBag.Products = await _db.Products.OrderBy(p => p.Code).ToListAsync();
            ViewBag.Page = paged.PageIndex;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            return View(paged.Items);
        }

        [Authorize(Roles = Roles.Admin + "," + Roles.Warehouse)]
        public async Task<IActionResult> Adjust()
        {
            ViewBag.Products = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Code).ToListAsync();
            return View();
        }

        [Authorize(Roles = Roles.Admin + "," + Roles.Warehouse)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(int productId, int quantityDelta, string? remark)
        {
            try
            {
                await _inventory.AdjustAsync(productId, quantityDelta, remark);
                TempData["Msg"] = "库存调整成功";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Err"] = ex.Message;
                ViewBag.Products = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Code).ToListAsync();
                return View();
            }
        }
    }
}
