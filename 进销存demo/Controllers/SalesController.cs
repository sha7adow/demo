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
    public class SalesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ISaleService _sale;
        private readonly IPdfService _pdf;
        private readonly IExcelService _excel;
        private readonly PagingOptions _paging;

        public SalesController(AppDbContext db, ISaleService sale, IPdfService pdf, IExcelService excel, IOptions<JxcOptions> jxc)
        {
            _db = db;
            _sale = sale;
            _pdf = pdf;
            _excel = excel;
            _paging = jxc.Value.Paging;
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ExportExcel()
        {
            var orders = await _db.SaleOrders
                .AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .OrderByDescending(o => o.Id)
                .ToListAsync();

            var rows = new List<SaleLineExcelRow>();
            foreach (var o in orders)
            {
                foreach (var it in o.Items)
                {
                    rows.Add(new SaleLineExcelRow(
                        o.OrderNo,
                        o.OrderDate,
                        o.Status.ToString(),
                        o.Customer?.Name ?? "",
                        it.Product?.Code ?? "",
                        it.Product?.Name ?? "",
                        it.Product?.Unit ?? "",
                        it.Quantity,
                        it.UnitPrice,
                        it.Subtotal));
                }
            }

            var cols = new[]
            {
                new ColumnSpec<SaleLineExcelRow>("单号", x => x.OrderNo),
                new ColumnSpec<SaleLineExcelRow>("日期", x => x.OrderDate.Date),
                new ColumnSpec<SaleLineExcelRow>("状态", x => x.Status),
                new ColumnSpec<SaleLineExcelRow>("客户", x => x.Customer),
                new ColumnSpec<SaleLineExcelRow>("商品编码", x => x.ProductCode),
                new ColumnSpec<SaleLineExcelRow>("商品名称", x => x.ProductName),
                new ColumnSpec<SaleLineExcelRow>("单位", x => x.Unit),
                new ColumnSpec<SaleLineExcelRow>("数量", x => x.Quantity),
                new ColumnSpec<SaleLineExcelRow>("单价", x => x.UnitPrice, "0.00"),
                new ColumnSpec<SaleLineExcelRow>("小计", x => x.LineAmount, "0.00"),
            };
            var bytes = _excel.Export(rows, "销售单明细", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "销售单明细.xlsx");
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult DownloadImportTemplate()
        {
            var cols = new[]
            {
                new ColumnSpec<SaleLineExcelRow>("单号", x => x.OrderNo),
                new ColumnSpec<SaleLineExcelRow>("日期", x => x.OrderDate.Date),
                new ColumnSpec<SaleLineExcelRow>("状态", x => x.Status),
                new ColumnSpec<SaleLineExcelRow>("客户", x => x.Customer),
                new ColumnSpec<SaleLineExcelRow>("商品编码", x => x.ProductCode),
                new ColumnSpec<SaleLineExcelRow>("商品名称", x => x.ProductName),
                new ColumnSpec<SaleLineExcelRow>("单位", x => x.Unit),
                new ColumnSpec<SaleLineExcelRow>("数量", x => x.Quantity),
                new ColumnSpec<SaleLineExcelRow>("单价", x => x.UnitPrice, "0.00"),
                new ColumnSpec<SaleLineExcelRow>("小计", x => x.LineAmount, "0.00"),
            };
            var bytes = _excel.Export(Array.Empty<SaleLineExcelRow>(), "销售单明细", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "销售单导入模板.xlsx");
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

            var importCols = new[]
            {
                new ImportColumn<SaleLineImportRow>("单号", (r, v) => r.OrderNo = v?.Trim() ?? "", true),
                new ImportColumn<SaleLineImportRow>("日期", (r, v) => r.DateRaw = v ?? "", true),
                new ImportColumn<SaleLineImportRow>("状态", (r, v) => r.StatusIgnored = v, false),
                new ImportColumn<SaleLineImportRow>("客户", (r, v) => r.CustomerName = v?.Trim() ?? "", true),
                new ImportColumn<SaleLineImportRow>("商品编码", (r, v) => r.ProductCode = v?.Trim() ?? "", true),
                new ImportColumn<SaleLineImportRow>("商品名称", (r, v) => r.ProductNameIgnored = v, false),
                new ImportColumn<SaleLineImportRow>("单位", (r, v) => r.UnitIgnored = v, false),
                new ImportColumn<SaleLineImportRow>("数量", (r, v) => r.QuantityRaw = v ?? "", true),
                new ImportColumn<SaleLineImportRow>("单价", (r, v) => r.UnitPriceRaw = v ?? "", true),
                new ImportColumn<SaleLineImportRow>("小计", (r, v) => r.LineAmountIgnored = v, false),
            };

            await using var stream = file.OpenReadStream();
            List<SaleLineImportRow> rows;
            try
            {
                rows = _excel.Import<SaleLineImportRow>(stream, importCols);
            }
            catch (Exception ex)
            {
                TempData["Err"] = "解析失败：" + ex.Message;
                return RedirectToAction(nameof(Index));
            }

            if (rows.Count == 0)
            {
                TempData["Err"] = "没有可导入的数据行";
                return RedirectToAction(nameof(Index));
            }

            var productByCode = await _db.Products.AsNoTracking()
                .Where(p => p.IsActive)
                .ToDictionaryAsync(p => p.Code, p => p.Id, StringComparer.Ordinal);
            var customerList = await _db.Customers.AsNoTracking().Select(c => new { c.Id, c.Name }).ToListAsync();
            var customerByName = customerList
                .GroupBy(c => c.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

            var groups = rows.GroupBy(r => r.OrderNo.Trim(), StringComparer.Ordinal).ToList();
            var errors = new List<string>();
            var orders = new List<SaleOrder>();
            foreach (var g in groups)
            {
                var ge = new List<string>();
                var orderNo = g.Key;
                if (string.IsNullOrWhiteSpace(orderNo))
                {
                    ge.Add("存在空单号分组");
                    errors.AddRange(ge);
                    continue;
                }

                if (await _db.SaleOrders.AnyAsync(o => o.OrderNo == orderNo))
                    ge.Add($"单号已存在：{orderNo}");

                var list = g.ToList();
                var customerNames = list.Select(r => r.CustomerName.Trim()).Distinct(StringComparer.Ordinal).ToList();
                if (customerNames.Count != 1 || string.IsNullOrWhiteSpace(customerNames[0]))
                    ge.Add($"单号「{orderNo}」：同一单内客户须一致且非空");
                else if (!customerByName.TryGetValue(customerNames[0], out var customerId))
                    ge.Add($"单号「{orderNo}」：找不到客户「{customerNames[0]}」");

                if (!TryParseImportDate(list[0].DateRaw, out var orderDate))
                    ge.Add($"单号「{orderNo}」：日期无效「{list[0].DateRaw}」");

                foreach (var r in list)
                {
                    if (!int.TryParse(r.QuantityRaw, out var qty) || qty <= 0)
                        ge.Add($"单号「{orderNo}」商品「{r.ProductCode}」：数量须为正整数");
                    if (!decimal.TryParse(r.UnitPriceRaw, out var price) || price < 0)
                        ge.Add($"单号「{orderNo}」商品「{r.ProductCode}」：单价无效");
                    if (!productByCode.ContainsKey(r.ProductCode))
                        ge.Add($"单号「{orderNo}」：商品编码不存在或未启用「{r.ProductCode}」");
                }

                if (ge.Count == 0
                    && customerNames.Count == 1
                    && customerByName.TryGetValue(customerNames[0], out var cid)
                    && TryParseImportDate(list[0].DateRaw, out var od))
                {
                    var items = new List<SaleOrderItem>();
                    foreach (var r in list)
                    {
                        int.TryParse(r.QuantityRaw, out var qty);
                        decimal.TryParse(r.UnitPriceRaw, out var price);
                        productByCode.TryGetValue(r.ProductCode, out var pid);
                        items.Add(new SaleOrderItem
                        {
                            ProductId = pid,
                            Quantity = qty,
                            UnitPrice = price
                        });
                    }

                    orders.Add(new SaleOrder
                    {
                        OrderNo = orderNo,
                        CustomerId = cid,
                        OrderDate = od,
                        Items = items
                    });
                }

                errors.AddRange(ge);
            }

            if (errors.Count > 0)
            {
                TempData["Err"] = string.Join("；", errors.Distinct());
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var n = await _sale.CreateManyFromImportAsync(orders);
                TempData["Msg"] = $"导入成功：共创建 {n} 张销售草稿单（单号与 Excel 一致，请至列表确认后「确认出库」）";
            }
            catch (Exception ex)
            {
                TempData["Err"] = "导入失败（已回滚）：" + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private static bool TryParseImportDate(string? raw, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (DateTime.TryParse(raw, out date))
            {
                date = date.Date;
                return true;
            }

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> Print(int id)
        {
            var o = await _sale.GetAsync(id);
            if (o == null) return NotFound();
            var bytes = _pdf.RenderSale(o);
            return File(bytes, "application/pdf", $"{o.OrderNo}.pdf");
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
                query.OrderByDescending(o => o.Id), q.Page, q.PageSize, _paging);

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

            if (!model.CustomerId.HasValue || model.CustomerId.Value <= 0)
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
