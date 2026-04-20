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
    [Authorize(Roles = Roles.Admin + "," + Roles.Salesperson)]
    public class CustomersController : Controller
    {
        private readonly AppDbContext _db;
        public CustomersController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index([FromQuery] CustomerQuery q)
        {
            var query = _db.Customers.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q.Keyword))
                query = query.Where(c => c.Name.Contains(q.Keyword)
                    || (c.Contact != null && c.Contact.Contains(q.Keyword))
                    || (c.Phone != null && c.Phone.Contains(q.Keyword)));

            var paged = await PagedList<Customer>.CreateAsync(query.OrderBy(c => c.Id), q.Page, q.PageSize);

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
