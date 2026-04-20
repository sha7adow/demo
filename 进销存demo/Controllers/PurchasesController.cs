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
    [Authorize(Roles = Roles.Admin + "," + Roles.Purchaser)]
    public class PurchasesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPurchaseService _purchase;

        public PurchasesController(AppDbContext db, IPurchaseService purchase)
        {
            _db = db;
            _purchase = purchase;
        }

        public async Task<IActionResult> Index([FromQuery] PurchaseQuery q)
        {
            var query = _db.PurchaseOrders.Include(o => o.Supplier).AsQueryable();

            if (!string.IsNullOrWhiteSpace(q.Keyword))
                query = query.Where(o => o.OrderNo.Contains(q.Keyword));
            if (q.SupplierId.HasValue)
                query = query.Where(o => o.SupplierId == q.SupplierId);
            if (q.Status.HasValue)
                query = query.Where(o => o.Status == q.Status);
            if (q.DateFrom.HasValue)
                query = query.Where(o => o.OrderDate >= q.DateFrom.Value.Date);
            if (q.DateTo.HasValue)
            {
                var end = q.DateTo.Value.Date.AddDays(1);
                query = query.Where(o => o.OrderDate < end);
            }

            var paged = await PagedList<PurchaseOrder>.CreateAsync(
                query.OrderByDescending(o => o.Id), q.Page, q.PageSize);

            ViewBag.Query = q;
            ViewBag.Suppliers = await _db.Suppliers.OrderBy(s => s.Name).ToListAsync();
            ViewBag.Page = paged.PageIndex;
            ViewBag.PageSize = paged.PageSize;
            ViewBag.TotalPages = paged.TotalPages;
            ViewBag.TotalCount = paged.TotalCount;
            return View(paged.Items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var order = await _purchase.GetAsync(id);
            return order == null ? NotFound() : View(order);
        }

        public async Task<IActionResult> Create()
        {
            await LoadSelectsAsync();
            return View(new PurchaseOrder { OrderNo = await _purchase.GenerateOrderNoAsync() });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            PurchaseOrder model,
            int[] productIds,
            int[] quantities,
            decimal[] unitPrices)
        {
            BuildItems(model, productIds, quantities, unitPrices);

            if (model.SupplierId <= 0)
                ModelState.AddModelError(nameof(PurchaseOrder.SupplierId), "请选择供应商");
            if (model.Items.Count == 0)
                ModelState.AddModelError(string.Empty, "请至少添加一条明细");

            if (!ModelState.IsValid)
            {
                await LoadSelectsAsync();
                return View(model);
            }

            await _purchase.CreateAsync(model);
            TempData["Msg"] = $"采购单 {model.OrderNo} 已创建（草稿）";
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            try
            {
                await _purchase.ConfirmAsync(id);
                TempData["Msg"] = "采购入库成功，库存已更新";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Return(int id, string? remark)
        {
            try
            {
                await _purchase.ReturnAsync(id, remark);
                TempData["Msg"] = "采购退货成功，库存已冲减";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _purchase.CancelAsync(id);
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
                await _purchase.DeleteAsync(id);
                TempData["Msg"] = "已删除";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Index));
        }

        private static void BuildItems(PurchaseOrder model, int[] productIds, int[] quantities, decimal[] unitPrices)
        {
            model.Items.Clear();
            if (productIds == null) return;
            for (int i = 0; i < productIds.Length; i++)
            {
                if (productIds[i] <= 0) continue;
                var qty = i < quantities.Length ? quantities[i] : 0;
                var price = i < unitPrices.Length ? unitPrices[i] : 0m;
                if (qty <= 0) continue;
                model.Items.Add(new PurchaseOrderItem
                {
                    ProductId = productIds[i],
                    Quantity = qty,
                    UnitPrice = price
                });
            }
        }

        private async Task LoadSelectsAsync()
        {
            ViewBag.Suppliers = await _db.Suppliers.OrderBy(s => s.Name).ToListAsync();
            ViewBag.Products = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Code).ToListAsync();
        }
    }
}
