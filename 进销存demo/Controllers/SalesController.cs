using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using 进销存demo.Data;
using 进销存demo.Models;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Identity;
using 进销存demo.Models.Queries;
using 进销存demo.Services;

namespace 进销存demo.Controllers
{
    [Authorize(Roles = Roles.Admin + "," + Roles.Salesperson)]
    public class SalesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ISaleService _sale;

        public SalesController(AppDbContext db, ISaleService sale)
        {
            _db = db;
            _sale = sale;
        }

        public async Task<IActionResult> Index([FromQuery] SaleQuery q)
        {
            var query = _db.SaleOrders.Include(o => o.Customer).AsQueryable();

            if (!string.IsNullOrWhiteSpace(q.Keyword))
                query = query.Where(o => o.OrderNo.Contains(q.Keyword));
            if (q.CustomerId.HasValue)
                query = query.Where(o => o.CustomerId == q.CustomerId);
            if (q.Status.HasValue)
                query = query.Where(o => o.Status == q.Status);
            if (q.DateFrom.HasValue)
                query = query.Where(o => o.OrderDate >= q.DateFrom.Value.Date);
            if (q.DateTo.HasValue)
            {
                var end = q.DateTo.Value.Date.AddDays(1);
                query = query.Where(o => o.OrderDate < end);
            }

            var paged = await PagedList<SaleOrder>.CreateAsync(
                query.OrderByDescending(o => o.Id), q.Page, q.PageSize);

            ViewBag.Query = q;
            ViewBag.Customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Page = paged.PageIndex;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            return View(paged.Items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _sale.GetAsync(id);
            return order == null ? NotFound() : View(order);
        }

        public async Task<IActionResult> Create()
        {
            await LoadSelectsAsync();
            return View(new SaleOrder { OrderNo = await _sale.GenerateOrderNoAsync() });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            SaleOrder model,
            int[] productIds,
            int[] quantities,
            decimal[] unitPrices)
        {
            BuildItems(model, productIds, quantities, unitPrices);

            if (model.CustomerId <= 0)
                ModelState.AddModelError(nameof(SaleOrder.CustomerId), "请选择客户");
            if (model.Items.Count == 0)
                ModelState.AddModelError(string.Empty, "请至少添加一条明细");

            if (!ModelState.IsValid)
            {
                await LoadSelectsAsync();
                return View(model);
            }

            await _sale.CreateAsync(model);
            TempData["Msg"] = $"销售单 {model.OrderNo} 已创建（草稿）";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            try
            {
                await _sale.ConfirmAsync(id);
                TempData["Msg"] = "销售出库成功，库存已扣减";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id, string? remark)
        {
            try
            {
                await _sale.ReturnAsync(id, remark);
                TempData["Msg"] = "销售退货成功，库存已补回";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _sale.CancelAsync(id);
                TempData["Msg"] = "已取消";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = Roles.Admin)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _sale.DeleteAsync(id);
                TempData["Msg"] = "已删除";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        private static void BuildItems(SaleOrder model, int[] productIds, int[] quantities, decimal[] unitPrices)
        {
            model.Items.Clear();
            if (productIds == null) return;
            for (int i = 0; i < productIds.Length; i++)
            {
                if (productIds[i] <= 0) continue;
                var qty = i < quantities.Length ? quantities[i] : 0;
                var price = i < unitPrices.Length ? unitPrices[i] : 0m;
                if (qty <= 0) continue;
                model.Items.Add(new SaleOrderItem
                {
                    ProductId = productIds[i],
                    Quantity = qty,
                    UnitPrice = price
                });
            }
        }

        private async Task LoadSelectsAsync()
        {
            ViewBag.Customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Products = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Code).ToListAsync();
        }
    }
}
