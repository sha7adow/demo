using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;
using 进销存demo.Services;

namespace 进销存demo.Controllers
{
    public class InventoryController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;

        public InventoryController(AppDbContext db, IInventoryService inventory)
        {
            _db = db;
            _inventory = inventory;
        }

        // 库存清单：可按编码/名称搜索，可勾选「仅看告警」
        public async Task<IActionResult> Index(string? keyword, bool onlyWarning = false)
        {
            var q = _db.Products.AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(p => p.Code.Contains(keyword) || p.Name.Contains(keyword));
            if (onlyWarning)
                q = q.Where(p => p.Stock <= p.SafetyStock);

            ViewBag.Keyword = keyword;
            ViewBag.OnlyWarning = onlyWarning;
            return View(await q.OrderBy(p => p.Code).ToListAsync());
        }

        // 库存流水
        public async Task<IActionResult> Transactions(int? productId)
        {
            var q = _db.StockTransactions.Include(t => t.Product).AsQueryable();
            if (productId.HasValue)
                q = q.Where(t => t.ProductId == productId.Value);

            ViewBag.Products = await _db.Products.OrderBy(p => p.Code).ToListAsync();
            ViewBag.ProductId = productId;
            return View(await q.OrderByDescending(t => t.Id).Take(500).ToListAsync());
        }

        // 库存调整：手工 + / -
        public async Task<IActionResult> Adjust()
        {
            ViewBag.Products = await _db.Products.OrderBy(p => p.Code).ToListAsync();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Adjust(int productId, int quantityDelta, string? remark)
        {
            try
            {
                if (productId <= 0) throw new InvalidOperationException("请选择商品");
                if (quantityDelta == 0) throw new InvalidOperationException("调整数量不能为 0");

                await _inventory.ApplyStockChangeAsync(
                    productId,
                    quantityDelta,
                    StockChangeType.Adjust,
                    refNo: null,
                    remark: string.IsNullOrWhiteSpace(remark) ? "手工调整" : remark);

                TempData["Msg"] = "库存调整成功";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Err"] = ex.Message;
                ViewBag.Products = await _db.Products.OrderBy(p => p.Code).ToListAsync();
                return View();
            }
        }
    }
}
