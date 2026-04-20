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
    [Authorize(Roles = Roles.Admin + "," + Roles.Purchaser)]
    public class SuppliersController : Controller
    {
        private readonly AppDbContext _db;
        public SuppliersController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index([FromQuery] SupplierQuery q)
        {
            var query = _db.Suppliers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q.Keyword))
                query = query.Where(s => s.Name.Contains(q.Keyword)
                    || (s.Contact != null && s.Contact.Contains(q.Keyword))
                    || (s.Phone != null && s.Phone.Contains(q.Keyword)));

            var paged = await PagedList<Supplier>.CreateAsync(query.OrderBy(s => s.Id), q.Page, q.PageSize);

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
