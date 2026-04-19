using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Controllers
{
    public class CustomersController : Controller
    {
        private readonly AppDbContext _db;
        public CustomersController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index() =>
            View(await _db.Customers.OrderBy(x => x.Id).ToListAsync());

        public IActionResult Create() => View(new Customer());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model)
        {
            if (!ModelState.IsValid) return View(model);
            model.CreatedAt = DateTime.Now;
            _db.Customers.Add(model);
            await _db.SaveChangesAsync();
            TempData["Msg"] = "创建成功";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.Customers.FindAsync(id);
            return c == null ? NotFound() : View(c);
        }

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _db.Customers.FindAsync(id);
            if (c != null)
            {
                _db.Customers.Remove(c);
                try { await _db.SaveChangesAsync(); }
                catch (DbUpdateException) { TempData["Err"] = "该客户已被销售单引用，无法删除"; return RedirectToAction(nameof(Index)); }
            }
            TempData["Msg"] = "删除成功";
            return RedirectToAction(nameof(Index));
        }
    }
}
