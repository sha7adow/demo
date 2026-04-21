using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using 进销存demo.Models.Identity;
using 进销存demo.Services;

namespace 进销存demo.Controllers
{
    [Authorize]
    public class StocktakesController : Controller
    {
        private readonly IStocktakeService _stocktake;
        private readonly IPdfService _pdf;

        public StocktakesController(IStocktakeService stocktake, IPdfService pdf)
        {
            _stocktake = stocktake;
            _pdf = pdf;
        }

        [HttpGet]
        public async Task<IActionResult> Print(int id)
        {
            var st = await _stocktake.GetAsync(id);
            if (st == null) return NotFound();
            var bytes = _pdf.RenderStocktake(st);
            return File(bytes, "application/pdf", $"{st.OrderNo}.pdf");
        }

        public async Task<IActionResult> Index() => View(await _stocktake.ListAsync());

        public async Task<IActionResult> Details(int id)
        {
            var st = await _stocktake.GetAsync(id);
            return st == null ? NotFound() : View(st);
        }

        [Authorize(Roles = Roles.Admin + "," + Roles.Warehouse)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string? remark)
        {
            try
            {
                var st = await _stocktake.CreateAsync(remark);
                TempData["Msg"] = $"盘点单 {st.OrderNo} 已生成(草稿)，请录入实盘数";
                return RedirectToAction(nameof(Details), new { id = st.Id });
            }
            catch (Exception ex)
            {
                TempData["Err"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [Authorize(Roles = Roles.Admin + "," + Roles.Warehouse)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(int id, int[] productIds, int[] actualQtys)
        {
            try
            {
                var dict = new Dictionary<int, int>();
                if (productIds != null && actualQtys != null)
                {
                    for (int i = 0; i < productIds.Length && i < actualQtys.Length; i++)
                        dict[productIds[i]] = actualQtys[i];
                }
                await _stocktake.UpdateActualQtyAsync(id, dict);
                TempData["Msg"] = "实盘数已保存";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = Roles.Admin + "," + Roles.Warehouse)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Confirm(int id)
        {
            try
            {
                await _stocktake.ConfirmAsync(id);
                TempData["Msg"] = "盘点确认完成，差异已生成调整流水";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }

        [Authorize(Roles = Roles.Admin + "," + Roles.Warehouse)]
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                await _stocktake.CancelAsync(id);
                TempData["Msg"] = "已取消";
            }
            catch (Exception ex) { TempData["Err"] = ex.Message; }
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
