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
    [Authorize(Roles = Roles.Admin + "," + Roles.Salesperson)]
    public class CustomersController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IExcelService _excel;
        private readonly PagingOptions _paging;

        public CustomersController(AppDbContext db, IExcelService excel, IOptions<JxcOptions> jxc)
        {
            _db = db;
            _excel = excel;
            _paging = jxc.Value.Paging;
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ExportExcel()
        {
            var rows = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
            var cols = new[]
            {
                new ColumnSpec<Customer>("名称", x => x.Name),
                new ColumnSpec<Customer>("联系人", x => x.Contact),
                new ColumnSpec<Customer>("电话", x => x.Phone),
                new ColumnSpec<Customer>("地址", x => x.Address),
                new ColumnSpec<Customer>("账期天", x => x.PaymentTermDays),
                new ColumnSpec<Customer>("备注", x => x.Remark),
            };
            var bytes = _excel.Export(rows, "客户", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "客户.xlsx");
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult DownloadImportTemplate()
        {
            var cols = new[]
            {
                new ColumnSpec<Customer>("名称", x => x.Name),
                new ColumnSpec<Customer>("联系人", x => x.Contact),
                new ColumnSpec<Customer>("电话", x => x.Phone),
                new ColumnSpec<Customer>("地址", x => x.Address),
                new ColumnSpec<Customer>("账期天", x => x.PaymentTermDays),
                new ColumnSpec<Customer>("备注", x => x.Remark),
            };
            var bytes = _excel.Export(Array.Empty<Customer>(), "客户", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "客户导入模板.xlsx");
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
                new ImportColumn<CustomerImportRow>("名称", (r, v) => r.Name = v?.Trim() ?? "", true),
                new ImportColumn<CustomerImportRow>("联系人", (r, v) => r.Contact = v, false),
                new ImportColumn<CustomerImportRow>("电话", (r, v) => r.Phone = v, false),
                new ImportColumn<CustomerImportRow>("地址", (r, v) => r.Address = v, false),
                new ImportColumn<CustomerImportRow>("账期天", (r, v) => r.PaymentTermDays = int.TryParse(v, out var n) && n > 0 ? n : 30, false),
                new ImportColumn<CustomerImportRow>("备注", (r, v) => r.Remark = v, false),
            };

            await using var stream = file.OpenReadStream();
            List<CustomerImportRow> rows;
            try
            {
                rows = _excel.Import<CustomerImportRow>(stream, cols);
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
                    if (await _db.Customers.AnyAsync(c => c.Name == name))
                    {
                        skipped++;
                        continue;
                    }

                    _db.Customers.Add(new Customer
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

        public async Task<IActionResult> Index([FromQuery] CustomerQuery q)
        {
            var query = _db.Customers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q.Keyword))
                query = query.Where(c => c.Name.Contains(q.Keyword)
                    || (c.Contact != null && c.Contact.Contains(q.Keyword))
                    || (c.Phone != null && c.Phone.Contains(q.Keyword)));

            var paged = await PagedList<Customer>.CreateAsync(query.OrderBy(c => c.Id), q.Page, q.PageSize, _paging);

            ViewBag.Query = q;
            ViewBag.Page = paged.PageIndex;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            return View(paged.Items);
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult Create() => View(new Customer());

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model)
        {
            if (!ModelState.IsValid) return View(model);
            if (model.PaymentTermDays <= 0) model.PaymentTermDays = 30;
            _db.Customers.Add(model);
            await _db.SaveChangesAsync();
            TempData["Msg"] = "创建成功";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.Customers.FindAsync(id);
            return c == null ? NotFound() : View(c);
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer model)
        {
            if (!ModelState.IsValid) return View(model);
            var c = await _db.Customers.FindAsync(model.Id);
            if (c == null) return NotFound();
            c.Name = model.Name;
            c.Contact = model.Contact;
            c.Phone = model.Phone;
            c.Address = model.Address;
            c.Remark = model.Remark;
            c.PaymentTermDays = model.PaymentTermDays <= 0 ? 30 : model.PaymentTermDays;
            await _db.SaveChangesAsync();
            TempData["Msg"] = "保存成功";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _db.Customers.FindAsync(id);
            if (c != null)
            {
                _db.Customers.Remove(c); // 软删除
                await _db.SaveChangesAsync();
                TempData["Msg"] = "已删除";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
