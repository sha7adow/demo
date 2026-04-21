using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;
using 进销存demo.Services;

namespace 进销存demo.Controllers
{
    [Authorize(Roles = Roles.Admin + "," + Roles.Salesperson)]
    public class ReceivablesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IOrderNoGenerator _orderNo;

        public ReceivablesController(AppDbContext db, IOrderNoGenerator orderNo)
        {
            _db = db;
            _orderNo = orderNo;
        }

        public async Task<IActionResult> Index(int? customerId, ReceivableStatus? status)
        {
            var q = _db.Receivables.Include(r => r.Customer).Include(r => r.SaleOrder).AsQueryable();
            if (customerId.HasValue)
                q = q.Where(r => r.CustomerId == customerId.Value);
            if (status.HasValue)
                q = q.Where(r => r.Status == status.Value);

            var list = await q.OrderByDescending(r => r.Id).ToListAsync();
            var today = DateTime.Today;
            decimal cur = 0, b0 = 0, b31 = 0, b61 = 0, b90 = 0;
            foreach (var r in list.Where(x => x.Status == ReceivableStatus.Outstanding))
            {
                var bal = r.Amount - r.Paid;
                if (r.DueDate.Date >= today) cur += bal;
                else
                {
                    var overdue = (today - r.DueDate.Date).Days;
                    if (overdue <= 30) b0 += bal;
                    else if (overdue <= 60) b31 += bal;
                    else if (overdue <= 90) b61 += bal;
                    else b90 += bal;
                }
            }

            ViewBag.Customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
            ViewBag.AgingCurrent = cur;
            ViewBag.Aging0_30 = b0;
            ViewBag.Aging31_60 = b31;
            ViewBag.Aging61_90 = b61;
            ViewBag.Aging90Plus = b90;
            ViewBag.CustomerId = customerId;
            ViewBag.Status = status;
            return View(list);
        }

        public async Task<IActionResult> Details(int id)
        {
            var r = await _db.Receivables
                .Include(x => x.Customer)
                .Include(x => x.SaleOrder)
                .Include(x => x.Receipts)
                .FirstOrDefaultAsync(x => x.Id == id);
            return r == null ? NotFound() : View(r);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, decimal amount, PaymentMethod method, DateTime paidDate, string? remark)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var r = await _db.Receivables.FirstOrDefaultAsync(x => x.Id == id)
                    ?? throw new InvalidOperationException("应收不存在");
                if (r.Status != ReceivableStatus.Outstanding)
                    throw new InvalidOperationException("仅未结清应收可收款");

                if (amount <= 0) throw new InvalidOperationException("金额必须大于 0");
                var max = r.Amount - r.Paid;
                if (amount > max) amount = max;

                var no = await _orderNo.NextAsync("RC");
                _db.PaymentReceipts.Add(new PaymentReceipt
                {
                    OrderNo = no,
                    ReceivableId = r.Id,
                    Amount = amount,
                    PaidDate = paidDate.Date,
                    Method = method,
                    Remark = remark,
                    CreatedAt = DateTime.Now
                });
                r.Paid += amount;
                r.UpdatedAt = DateTime.Now;
                if (r.Paid >= r.Amount)
                {
                    r.Paid = r.Amount;
                    r.Status = ReceivableStatus.Paid;
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
                TempData["Msg"] = "收款已登记";
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
