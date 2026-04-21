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
    [Authorize(Roles = Roles.Admin + "," + Roles.Purchaser)]
    public class PurchasesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IPurchaseService _purchase;
        private readonly IPdfService _pdf;
        private readonly IExcelService _excel;
        private readonly PagingOptions _paging;

        public PurchasesController(AppDbContext db, IPurchaseService purchase, IPdfService pdf, IExcelService excel, IOptions<JxcOptions> jxc)
        {
            _db = db;
            _purchase = purchase;
            _pdf = pdf;
            _excel = excel;
            _paging = jxc.Value.Paging;
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> ExportExcel()
        {
            var orders = await _db.PurchaseOrders
                .AsNoTracking()
                .Include(o => o.Supplier)
                .Include(o => o.Items).ThenInclude(i => i.Product)
                .OrderByDescending(o => o.Id)
                .ToListAsync();

            var rows = new List<PurchaseLineExcelRow>();
            foreach (var o in orders)
            {
                foreach (var it in o.Items)
                {
                    rows.Add(new PurchaseLineExcelRow(
                        o.OrderNo,
                        o.OrderDate,
                        o.Status.ToString(),
                        o.Supplier?.Name ?? "",
                        it.Product?.Code ?? "",
                        it.Product?.Name ?? "",
                        it.Product?.Unit ?? "",
                        it.Quantity,
                        it.UnitPrice,
                        it.Subtotal,
                        it.ProductionDate?.ToString("yyyy-MM-dd"),
                        it.BatchNo));
                }
            }

            var cols = new[]
            {
                new ColumnSpec<PurchaseLineExcelRow>("单号", x => x.OrderNo),
                new ColumnSpec<PurchaseLineExcelRow>("日期", x => x.OrderDate.Date),
                new ColumnSpec<PurchaseLineExcelRow>("状态", x => x.Status),
                new ColumnSpec<PurchaseLineExcelRow>("供应商", x => x.Supplier),
                new ColumnSpec<PurchaseLineExcelRow>("商品编码", x => x.ProductCode),
                new ColumnSpec<PurchaseLineExcelRow>("商品名称", x => x.ProductName),
                new ColumnSpec<PurchaseLineExcelRow>("单位", x => x.Unit),
                new ColumnSpec<PurchaseLineExcelRow>("数量", x => x.Quantity),
                new ColumnSpec<PurchaseLineExcelRow>("单价", x => x.UnitPrice, "0.00"),
                new ColumnSpec<PurchaseLineExcelRow>("小计", x => x.LineAmount, "0.00"),
                new ColumnSpec<PurchaseLineExcelRow>("生产日期", x => x.ProductionDate),
                new ColumnSpec<PurchaseLineExcelRow>("批次号", x => x.BatchNo),
            };
            var bytes = _excel.Export(rows, "采购单明细", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "采购单明细.xlsx");
        }

        [Authorize(Roles = Roles.Admin)]
        public IActionResult DownloadImportTemplate()
        {
            var cols = new[]
            {
                new ColumnSpec<PurchaseLineExcelRow>("单号", x => x.OrderNo),
                new ColumnSpec<PurchaseLineExcelRow>("日期", x => x.OrderDate.Date),
                new ColumnSpec<PurchaseLineExcelRow>("状态", x => x.Status),
                new ColumnSpec<PurchaseLineExcelRow>("供应商", x => x.Supplier),
                new ColumnSpec<PurchaseLineExcelRow>("商品编码", x => x.ProductCode),
                new ColumnSpec<PurchaseLineExcelRow>("商品名称", x => x.ProductName),
                new ColumnSpec<PurchaseLineExcelRow>("单位", x => x.Unit),
                new ColumnSpec<PurchaseLineExcelRow>("数量", x => x.Quantity),
                new ColumnSpec<PurchaseLineExcelRow>("单价", x => x.UnitPrice, "0.00"),
                new ColumnSpec<PurchaseLineExcelRow>("小计", x => x.LineAmount, "0.00"),
                new ColumnSpec<PurchaseLineExcelRow>("生产日期", x => x.ProductionDate),
                new ColumnSpec<PurchaseLineExcelRow>("批次号", x => x.BatchNo),
            };
            var bytes = _excel.Export(Array.Empty<PurchaseLineExcelRow>(), "采购单明细", cols);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "采购单导入模板.xlsx");
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
                new ImportColumn<PurchaseLineImportRow>("单号", (r, v) => r.OrderNo = v?.Trim() ?? "", true),
                new ImportColumn<PurchaseLineImportRow>("日期", (r, v) => r.DateRaw = v ?? "", true),
                new ImportColumn<PurchaseLineImportRow>("状态", (r, v) => r.StatusIgnored = v, false),
                new ImportColumn<PurchaseLineImportRow>("供应商", (r, v) => r.SupplierName = v?.Trim() ?? "", true),
                new ImportColumn<PurchaseLineImportRow>("商品编码", (r, v) => r.ProductCode = v?.Trim() ?? "", true),
                new ImportColumn<PurchaseLineImportRow>("商品名称", (r, v) => r.ProductNameIgnored = v, false),
                new ImportColumn<PurchaseLineImportRow>("单位", (r, v) => r.UnitIgnored = v, false),
                new ImportColumn<PurchaseLineImportRow>("数量", (r, v) => r.QuantityRaw = v ?? "", true),
                new ImportColumn<PurchaseLineImportRow>("单价", (r, v) => r.UnitPriceRaw = v ?? "", true),
                new ImportColumn<PurchaseLineImportRow>("小计", (r, v) => r.LineAmountIgnored = v, false),
                new ImportColumn<PurchaseLineImportRow>("生产日期", (r, v) => r.ProductionDateRaw = v, false),
                new ImportColumn<PurchaseLineImportRow>("批次号", (r, v) => r.BatchNo = string.IsNullOrWhiteSpace(v) ? null : v.Trim(), false),
            };

            await using var stream = file.OpenReadStream();
            List<PurchaseLineImportRow> rows;
            try
            {
                rows = _excel.Import<PurchaseLineImportRow>(stream, importCols);
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
            var supplierList = await _db.Suppliers.AsNoTracking().Select(s => new { s.Id, s.Name }).ToListAsync();
            var supplierByName = supplierList
                .GroupBy(s => s.Name, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

            var groups = rows.GroupBy(r => r.OrderNo.Trim(), StringComparer.Ordinal).ToList();
            var errors = new List<string>();
            var orders = new List<PurchaseOrder>();
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

                if (await _db.PurchaseOrders.AnyAsync(o => o.OrderNo == orderNo))
                    ge.Add($"单号已存在：{orderNo}");

                var list = g.ToList();
                var supplierNames = list.Select(r => r.SupplierName.Trim()).Distinct(StringComparer.Ordinal).ToList();
                if (supplierNames.Count != 1 || string.IsNullOrWhiteSpace(supplierNames[0]))
                    ge.Add($"单号「{orderNo}」：同一单内供应商须一致且非空");
                else if (!supplierByName.TryGetValue(supplierNames[0], out var supplierId))
                    ge.Add($"单号「{orderNo}」：找不到供应商「{supplierNames[0]}」");

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
                    if (!string.IsNullOrWhiteSpace(r.ProductionDateRaw) && !TryParseImportDate(r.ProductionDateRaw, out _))
                        ge.Add($"单号「{orderNo}」商品「{r.ProductCode}」：生产日期无法识别");
                }

                if (ge.Count == 0
                    && supplierNames.Count == 1
                    && supplierByName.TryGetValue(supplierNames[0], out var sid)
                    && TryParseImportDate(list[0].DateRaw, out var od))
                {
                    var items = new List<PurchaseOrderItem>();
                    foreach (var r in list)
                    {
                        int.TryParse(r.QuantityRaw, out var qty);
                        decimal.TryParse(r.UnitPriceRaw, out var price);
                        productByCode.TryGetValue(r.ProductCode, out var pid);
                        DateTime? prodDate = null;
                        if (!string.IsNullOrWhiteSpace(r.ProductionDateRaw) && TryParseImportDate(r.ProductionDateRaw, out var pd))
                            prodDate = pd.Date;
                        items.Add(new PurchaseOrderItem
                        {
                            ProductId = pid,
                            Quantity = qty,
                            UnitPrice = price,
                            ProductionDate = prodDate,
                            BatchNo = r.BatchNo
                        });
                    }

                    orders.Add(new PurchaseOrder
                    {
                        OrderNo = orderNo,
                        SupplierId = sid,
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
                var n = await _purchase.CreateManyFromImportAsync(orders);
                TempData["Msg"] = $"导入成功：共创建 {n} 张采购草稿单（单号与 Excel 一致，请至列表确认后「确认入库」）";
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
            var o = await _purchase.GetAsync(id);
            if (o == null) return NotFound();
            var bytes = _pdf.RenderPurchase(o);
            return File(bytes, "application/pdf", $"{o.OrderNo}.pdf");
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
                query.OrderByDescending(o => o.Id), q.Page, q.PageSize, _paging);

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
            decimal[] unitPrices,
            DateTime?[]? productionDates = null,
            string[]? batchNos = null)
        {
            BuildItems(model, productIds, quantities, unitPrices, productionDates, batchNos);

            if (!model.SupplierId.HasValue || model.SupplierId.Value <= 0)
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

        private static void BuildItems(
            PurchaseOrder model,
            int[] productIds,
            int[] quantities,
            decimal[] unitPrices,
            DateTime?[]? productionDates,
            string[]? batchNos)
        {
            model.Items.Clear();
            if (productIds == null) return;
            for (int i = 0; i < productIds.Length; i++)
            {
                if (productIds[i] <= 0) continue;
                var qty = i < quantities.Length ? quantities[i] : 0;
                var price = i < unitPrices.Length ? unitPrices[i] : 0m;
                if (qty <= 0) continue;
                DateTime? pd = null;
                if (productionDates != null && i < productionDates.Length)
                    pd = productionDates[i];
                string? bn = null;
                if (batchNos != null && i < batchNos.Length && !string.IsNullOrWhiteSpace(batchNos[i]))
                    bn = batchNos[i]!.Trim();
                model.Items.Add(new PurchaseOrderItem
                {
                    ProductId = productIds[i],
                    Quantity = qty,
                    UnitPrice = price,
                    ProductionDate = pd,
                    BatchNo = bn
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
