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
    [Authorize(Roles = Roles.Admin + "," + Roles.Purchaser)]
    public class SuppliersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IExcelService _excel;
        private readonly PagingOptions _paging;

        public SuppliersController(AppDbContext db, IExcelService excel, IOptions<JxcOptions> jxc)
        {
            _db = db;
            _excel = excel;
            _paging = jxc.Value.Paging;
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ExportExcel()
        {
            var rows = await _db.Suppliers.OrderBy(s => s.Name).ToListAsync();
            var cols = new[]
            {
                new ColumnSpec<Supplier>("名称", x => x.Name),
                new ColumnSpec<Supplier>("联系人", x => x.Contact),
                new ColumnSpec<Supplier>("电话", x => x.Phone),
                new ColumnSpec<Supplier>("地址", x => x.Address),
                new ColumnSpec<Supplier>("账期天", x => x.PaymentTermDays),
                new ColumnSpec<Supplier>("备注", x => x.Remark),
            };
            var bytes = _excel.Export(rows, "供应商", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "供应商.xlsx");
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult DownloadImportTemplate()
        {
            var cols = new[]
            {
                new ColumnSpec<Supplier>("名称", x => x.Name),
                new ColumnSpec<Supplier>("联系人", x => x.Contact),
                new ColumnSpec<Supplier>("电话", x => x.Phone),
                new ColumnSpec<Supplier>("地址", x => x.Address),
                new ColumnSpec<Supplier>("账期天", x => x.PaymentTermDays),
                new ColumnSpec<Supplier>("备注", x => x.Remark),
            };
            var bytes = _excel.Export(Array.Empty<Supplier>(), "供应商", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "供应商导入模板.xlsx");
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
                new ImportColumn<SupplierImportRow>("名称", (r, v) => r.Name = v?.Trim() ?? "", true),
                new ImportColumn<SupplierImportRow>("联系人", (r, v) => r.Contact = v, false),
                new ImportColumn<SupplierImportRow>("电话", (r, v) => r.Phone = v, false),
                new ImportColumn<SupplierImportRow>("地址", (r, v) => r.Address = v, false),
                new ImportColumn<SupplierImportRow>("账期天", (r, v) => r.PaymentTermDays = int.TryParse(v, out var n) && n > 0 ? n : 30, false),
                new ImportColumn<SupplierImportRow>("备注", (r, v) => r.Remark = v, false),
            };

            await using var stream = file.OpenReadStream();
            List<SupplierImportRow> rows;
            try
            {
                rows = _excel.Import<SupplierImportRow>(stream, cols);
            }
            catch (Exception ex)
            {
                TempData["Err"] = "解析失败：" + ex.Message;
                return RedirectToAction(nameof(Index));
            }

            var ok = 0;
            var skipped = 0;
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.Name))
                    {
                        skipped++;
                        continue;
                    }

                    var name = row.Name.Trim();
                    if (await _db.Suppliers.AnyAsync(s => s.Name == name))
                    {
                        skipped++;
                        continue;
                    }

                    _db.Suppliers.Add(new Supplier
                    {
                        Name = name,
                        Contact = row.Contact,
                        Phone = row.Phone,
                        Address = row.Address,
                        Remark = row.Remark,
                        PaymentTermDays = row.PaymentTermDays <= 0 ? 30 : row.PaymentTermDays
                    });
                    ok++;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                TempData["Msg"] = $"导入完成：成功 {ok} 条，跳过 {skipped} 条（名称为空或名称已存在）";
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["Err"] = "导入失败（已回滚）：" + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index([FromQuery] SupplierQuery q)
        {
            var query = _db.Suppliers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q.Keyword))
                query = query.Where(s => s.Name.Contains(q.Keyword)
                    || (s.Contact != null && s.Contact.Contains(q.Keyword))
                    || (s.Phone != null && s.Phone.Contains(q.Keyword)));

            var paged = await PagedList<Supplier>.CreateAsync(query.OrderBy(s => s.Id), q.Page, q.PageSize, _paging);

            ViewBag.Query = q;
            ViewBag.Page = paged.PageIndex;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            return View(paged.Items);
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult Create() => View(new Supplier());

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier model)
        {
            if (!ModelState.IsValid) return View(model);
            if (model.PaymentTermDays <= 0) model.PaymentTermDays = 30;
            _db.Suppliers.Add(model);
            await _db.SaveChangesAsync();
            TempData["Msg"] = "创建成功";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(int id)
        {
            var s = await _db.Suppliers.FindAsync(id);
            return s == null ? NotFound() : View(s);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Supplier model)
        {
            if (!ModelState.IsValid) return View(model);
            var s = await _db.Suppliers.FindAsync(model.Id);
            if (s == null) return NotFound();
            s.Name = model.Name;
            s.Contact = model.Contact;
            s.Phone = model.Phone;
            s.Address = model.Address;
            s.Remark = model.Remark;
            s.PaymentTermDays = model.PaymentTermDays <= 0 ? 30 : model.PaymentTermDays;
            await _db.SaveChangesAsync();
            TempData["Msg"] = "保存成功";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _db.Suppliers.FindAsync(id);
            if (s != null)
            {
                _db.Suppliers.Remove(s); // 软删除（由拦截器处理）
                await _db.SaveChangesAsync();
                TempData["Msg"] = "已删除";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
