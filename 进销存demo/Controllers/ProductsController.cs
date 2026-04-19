using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Controllers
{
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;
        public ProductsController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index(string? keyword)
        {
            var q = _db.Products.AsQueryable();
            if (!string.IsNullOrWhiteSpace(keyword))
                q = q.Where(p => p.Code.Contains(keyword) || p.Name.Contains(keyword));
            ViewBag.Keyword = keyword;
            return View(await q.OrderBy(p => p.Code).ToListAsync());
        }

        public IActionResult Create() => View(new Product());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model)
        {
            if (!ModelState.IsValid) return View(model);
            if (await _db.Products.AnyAsync(p => p.Code == model.Code))
            {
                ModelState.AddModelError(nameof(Product.Code), "商品编码已存在");
                return View(model);
            }
            model.CreatedAt = DateTime.Now;
            _db.Products.Add(model);
            await _db.SaveChangesAsync();
            TempData["Msg"] = "创建成功";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var p = await _db.Products.FindAsync(id);
            return p == null ? NotFound() : View(p);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product model)
        {
            if (!ModelState.IsValid) return View(model);
            var p = await _db.Products.FindAsync(model.Id);
            if (p == null) return NotFound();

            p.Code = model.Code;
            p.Name = model.Name;
            p.Unit = model.Unit;
            p.PurchasePrice = model.PurchasePrice;
            p.SalePrice = model.SalePrice;
            p.SafetyStock = model.SafetyStock;
            p.Remark = model.Remark;

            await _db.SaveChangesAsync();
            TempData["Msg"] = "保存成功";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p != null)
            {
                _db.Products.Remove(p);
                try { await _db.SaveChangesAsync(); }
                catch (DbUpdateException) { TempData["Err"] = "该商品已被订单引用，无法删除"; return RedirectToAction(nameof(Index)); }
            }
            TempData["Msg"] = "删除成功";
            return RedirectToAction(nameof(Index));
        }
    }
}
