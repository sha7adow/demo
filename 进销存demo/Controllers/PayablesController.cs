using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;
using 进销存demo.Services;

namespace 进销存demo.Controllers
{
    [Authorize(Roles = Roles.Admin + "," + Roles.Purchaser)]
    public class PayablesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IOrderNoGenerator _orderNo;

        public PayablesController(AppDbContext db, IOrderNoGenerator orderNo)
        {
            _db = db;
            _orderNo = orderNo;
        }

        public async Task<IActionResult> Index(int? supplierId, PayableStatus? status)
        {
            var q = _db.Payables.Include(p => p.Supplier).Include(p => p.PurchaseOrder).AsQueryable();
            if (supplierId.HasValue)
                q = q.Where(p => p.SupplierId == supplierId.Value);
            if (status.HasValue)
                q = q.Where(p => p.Status == status.Value);

            var list = await q.OrderByDescending(p => p.Id).ToListAsync();
            ViewBag.Suppliers = await _db.Suppliers.OrderBy(s => s.Name).ToListAsync();
            ViewBag.SupplierId = supplierId;
            ViewBag.Status = status;
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var p = await _db.Payables
                .Include(x => x.Supplier)
                .Include(x => x.PurchaseOrder)
                .Include(x => x.Vouchers)
                .FirstOrDefaultAsync(x => x.Id == id);
            return p == null ? NotFound() : View(p);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, decimal amount, PaymentMethod method, DateTime paidDate, string? remark)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var p = await _db.Payables.FirstOrDefaultAsync(x => x.Id == id)
                    ?? throw new InvalidOperationException("应付不存在");
                if (p.Status != PayableStatus.Outstanding)
                    throw new InvalidOperationException("仅未结清应付可付款");

                if (amount <= 0) throw new InvalidOperationException("金额必须大于 0");
                var max = p.Amount - p.Paid;
                if (amount > max) amount = max;

                var no = await _orderNo.NextAsync("PV");
                _db.PaymentVouchers.Add(new PaymentVoucher
                {
                    OrderNo = no,
                    PayableId = p.Id,
                    Amount = amount,
                    PaidDate = paidDate.Date,
                    Method = method,
                    Remark = remark,
                    CreatedAt = DateTime.Now
                });
                p.Paid += amount;
                p.UpdatedAt = DateTime.Now;
                if (p.Paid >= p.Amount)
                {
                    p.Paid = p.Amount;
                    p.Status = PayableStatus.Paid;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                TempData["Msg"] = "付款已登记";
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                TempData["Err"] = ex.Message;
            }
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
