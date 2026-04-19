using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;

namespace 进销存demo.Controllers
{
    public class SuppliersController : Controller
    {
        private readonly AppDbContext _db;
        public SuppliersController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index() =>
            View(await _db.Suppliers.OrderBy(x => x.Id).ToListAsync());

        public IActionResult Create() => View(new Supplier());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier model)
        {
            if (!ModelState.IsValid) return View(model);
            model.CreatedAt = DateTime.Now;
            _db.Suppliers.Add(model);
            await _db.SaveChangesAsync();
            TempData["Msg"] = "创建成功";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var s = await _db.Suppliers.FindAsync(id);
            return s == null ? NotFound() : View(s);
        }

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

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _db.Suppliers.FindAsync(id);
            if (s != null)
            {
                _db.Suppliers.Remove(s);
                try { await _db.SaveChangesAsync(); }
                catch (DbUpdateException) { TempData["Err"] = "该供应商已被采购单引用，无法删除"; return RedirectToAction(nameof(Index)); }
            }
            TempData["Msg"] = "删除成功";
            return RedirectToAction(nameof(Index));
        }
    }
}
