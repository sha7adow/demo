using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;
using 进销存demo.Models.Queries;

namespace 进销存demo.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;
        public ProductsController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index([FromQuery] ProductQuery q)
        {
            var query = _db.Products.Include(p => p.Category).AsQueryable();

            if (!string.IsNullOrWhiteSpace(q.Keyword))
                query = query.Where(p => p.Code.Contains(q.Keyword) || p.Name.Contains(q.Keyword) || (p.Barcode != null && p.Barcode.Contains(q.Keyword)));

            if (q.CategoryId.HasValue)
                query = query.Where(p => p.CategoryId == q.CategoryId);

            if (q.OnlyWarning == true)
                query = query.Where(p => p.Stock <= p.SafetyStock);

            if (q.OnlyActive == true)
                query = query.Where(p => p.IsActive);

            var paged = await PagedList<Product>.CreateAsync(
                query.OrderBy(p => p.Code), q.Page, q.PageSize);

            ViewBag.Query = q;
            ViewBag.Categories = await _db.ProductCategories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Page = paged.PageIndex;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;

            return View(paged.Items);
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Create()
        {
            await LoadCategoriesAsync();
            return View(new Product());
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product model)
        {
            if (!ModelState.IsValid) { await LoadCategoriesAsync(); return View(model); }

            if (await _db.Products.AnyAsync(p => p.Code == model.Code))
            {
                ModelState.AddModelError(nameof(Product.Code), "商品编码已存在");
                await LoadCategoriesAsync();
                return View(model);
            }
            _db.Products.Add(model);
            await _db.SaveChangesAsync();
            TempData["Msg"] = "创建成功";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(int id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p == null) return NotFound();
            await LoadCategoriesAsync();
            return View(p);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Product model)
        {
            if (!ModelState.IsValid) { await LoadCategoriesAsync(); return View(model); }

            var p = await _db.Products.FindAsync(model.Id);
            if (p == null) return NotFound();

            try
            {
                p.Code = model.Code;
                p.Name = model.Name;
                p.Unit = model.Unit;
                p.Barcode = model.Barcode;
                p.CategoryId = model.CategoryId;
                p.PurchasePrice = model.PurchasePrice;
                p.SalePrice = model.SalePrice;
                p.SafetyStock = model.SafetyStock;
                p.IsActive = model.IsActive;
                p.Remark = model.Remark;
                // 乐观锁：用 Original Value 匹配 RowVersion
                _db.Entry(p).Property(x => x.RowVersion).OriginalValue = model.RowVersion;

                await _db.SaveChangesAsync();
                TempData["Msg"] = "保存成功";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "该商品已被其他用户修改，请返回列表刷新后重试");
                await LoadCategoriesAsync();
                return View(model);
            }
        }

        /// <summary>软删除：置 IsDeleted 而不是物理删除。</summary>
        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p != null)
            {
                _db.Products.Remove(p); // 拦截器会转成 Modified + 置 IsDeleted
                await _db.SaveChangesAsync();
                TempData["Msg"] = "已删除";
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task LoadCategoriesAsync()
        {
            ViewBag.Categories = await _db.ProductCategories.OrderBy(c => c.Name).ToListAsync();
        }
    }
}
