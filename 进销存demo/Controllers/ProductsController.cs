using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 进销存demo.Data;
using 进销存demo.Models;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;
using 进销存demo.Models.Options;
using 进销存demo.Models.Queries;
using 进销存demo.Services;

namespace 进销存demo.Controllers
{
    [Authorize]
    public class ProductsController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IExcelService _excel;
        private readonly IBatchInventoryService _batch;
        private readonly PagingOptions _paging;

        public ProductsController(AppDbContext db, IExcelService excel, IBatchInventoryService batch, IOptions<JxcOptions> jxc)
        {
            _db = db;
            _excel = excel;
            _batch = batch;
            _paging = jxc.Value.Paging;
        }

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
                query.OrderBy(p => p.Code), q.Page, q.PageSize, _paging);

            ViewBag.Query = q;
            ViewBag.Categories = await _db.ProductCategories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Page = paged.PageIndex;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;

            return View(paged.Items);
        }

        [Authorize(Roles = Roles.Admin + "," + Roles.Warehouse)]
        public async Task<IActionResult> Batches(int id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p == null) return NotFound();
            var list = await _db.ProductBatches
                .Where(b => b.ProductId == id)
                .OrderBy(b => b.ExpiryDate)
                .ToListAsync();
            ViewBag.Product = p;
            return View(list);
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ExportExcel()
        {
            var rows = await _db.Products.Include(p => p.Category).OrderBy(p => p.Code).ToListAsync();
            var cols = new[]
            {
                new ColumnSpec<Product>("编码", x => x.Code),
                new ColumnSpec<Product>("名称", x => x.Name),
                new ColumnSpec<Product>("单位", x => x.Unit),
                new ColumnSpec<Product>("采购价", x => x.PurchasePrice, "0.00"),
                new ColumnSpec<Product>("销售价", x => x.SalePrice, "0.00"),
                new ColumnSpec<Product>("安全库存", x => x.SafetyStock),
                new ColumnSpec<Product>("启用批次", x => x.TrackBatch ? 1 : 0),
                new ColumnSpec<Product>("保质期天", x => x.ShelfLifeDays),
                new ColumnSpec<Product>("分类", x => x.Category?.Name),
                new ColumnSpec<Product>("条码", x => x.Barcode),
            };
            var bytes = _excel.Export(rows, "商品", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "商品.xlsx");
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult DownloadProductTemplate()
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
            var bytes = _excel.Export(Array.Empty<Product>(), "商品", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "商品导入模板.xlsx");
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Err"] = "请选择文件";
                return RedirectToAction(nameof(Index));
            }

            var cols = new[]
            {
                new ImportColumn<ProductImportRow>("编码", (p, v) => p.Code = v ?? "", true),
                new ImportColumn<ProductImportRow>("名称", (p, v) => p.Name = v ?? "", true),
                new ImportColumn<ProductImportRow>("单位", (p, v) => p.Unit = string.IsNullOrWhiteSpace(v) ? "个" : v!, false),
                new ImportColumn<ProductImportRow>("采购价", (p, v) => p.PurchasePrice = decimal.TryParse(v, out var d) ? d : 0, false),
                new ImportColumn<ProductImportRow>("销售价", (p, v) => p.SalePrice = decimal.TryParse(v, out var d) ? d : 0, false),
                new ImportColumn<ProductImportRow>("安全库存", (p, v) => p.SafetyStock = int.TryParse(v, out var n) ? n : 0, false),
                new ImportColumn<ProductImportRow>("启用批次", (p, v) => p.TrackBatch = v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase), false),
                new ImportColumn<ProductImportRow>("保质期天", (p, v) => p.ShelfLifeDays = int.TryParse(v, out var d) ? d : 365, false),
                new ImportColumn<ProductImportRow>("分类", (p, v) => p.CategoryName = v, false),
                new ImportColumn<ProductImportRow>("条码", (p, v) => p.Barcode = v, false),
            };

            await using var stream = file.OpenReadStream();
            List<ProductImportRow> rows;
            try
            {
                rows = _excel.Import<ProductImportRow>(stream, cols);
            }
            catch (Exception ex)
            {
                TempData["Err"] = "解析失败：" + ex.Message;
                return RedirectToAction(nameof(Index));
            }

            var skipped = 0;
            var ok = 0;
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.Code)) { skipped++; continue; }
                    if (await _db.Products.AnyAsync(p => p.Code == row.Code)) { skipped++; continue; }

                    int? catId = null;
                    if (!string.IsNullOrWhiteSpace(row.CategoryName))
                    {
                        catId = await _db.ProductCategories.Where(c => c.Name == row.CategoryName).Select(c => (int?)c.Id).FirstOrDefaultAsync();
                    }

                    var p = new Product
                    {
                        Code = row.Code.Trim(),
                        Name = row.Name.Trim(),
                        Unit = row.Unit,
                        PurchasePrice = row.PurchasePrice,
                        SalePrice = row.SalePrice,
                        SafetyStock = row.SafetyStock,
                        TrackBatch = row.TrackBatch,
                        ShelfLifeDays = row.ShelfLifeDays <= 0 ? 365 : row.ShelfLifeDays,
                        CategoryId = catId,
                        Barcode = row.Barcode,
                        Stock = 0,
                        IsActive = true
                    };
                    _db.Products.Add(p);
                    ok++;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                TempData["Msg"] = $"导入完成：成功 {ok} 条，跳过 {skipped} 条（编码重复或空行）";
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["Err"] = "导入失败（已回滚）：" + ex.Message;
            }

            return RedirectToAction(nameof(Index));
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

            var initialStock = model.Stock;
            if (model.TrackBatch)
                model.Stock = 0;

            _db.Products.Add(model);
            await _db.SaveChangesAsync();

            if (model.TrackBatch && initialStock > 0)
            {
                var pd = DateTime.Today;
                var shelf = model.ShelfLifeDays <= 0 ? 365 : model.ShelfLifeDays;
                await _batch.ReceiveAsync(
                    model.Id,
                    initialStock,
                    model.PurchasePrice,
                    null,
                    pd,
                    pd.AddDays(shelf),
                    null,
                    null,
                    StockChangeType.Purchase);
            }

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
                p.ShelfLifeDays = model.ShelfLifeDays <= 0 ? 365 : model.ShelfLifeDays;
                p.TrackBatch = model.TrackBatch;
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

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p != null)
            {
                _db.Products.Remove(p);
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
