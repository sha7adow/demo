using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Options;

namespace 进销存demo.Services
{
    public class PdfService : IPdfService
    {
        private readonly JxcOptions _opt;
        private readonly IHttpContextAccessor _http;

        public PdfService(IOptions<JxcOptions> opt, IHttpContextAccessor http)
        {
            _opt = opt.Value;
            _http = http;
        }

        private string Font => string.IsNullOrWhiteSpace(_opt.Company.PdfFontFamily)
            ? "Microsoft YaHei"
            : _opt.Company.PdfFontFamily;

        private string HandlerName =>
            _http.HttpContext?.User?.Identity?.Name ?? "—";

        public byte[] RenderPurchase(PurchaseOrder order) =>
            Document.Create(c =>
            {
                c.Page(p =>
                {
                    p.Margin(36);
                    p.DefaultTextStyle(t => t.FontFamily(Font).FontSize(10));
                    p.Header().Column(col =>
                    {
                        col.Item().Text(_opt.Company.Name).SemiBold().FontSize(14);
                        col.Item().Text($"采购单 {order.OrderNo}").SemiBold();
                        col.Item().Text($"日期：{order.OrderDate:yyyy-MM-dd}  状态：{order.Status}");
                        col.Item().Text($"供应商：{order.Supplier?.Name}");
                        col.Item().Text($"电话：{order.Supplier?.Phone ?? "—"}  地址：{order.Supplier?.Address ?? "—"}");
                    });
                    p.Content().Column(main =>
                    {
                        main.Item().Table(t =>
                        {
                            t.ColumnsDefinition(d =>
                            {
                                d.RelativeColumn(2);
                                d.RelativeColumn(3);
                                d.ConstantColumn(48);
                                d.ConstantColumn(72);
                                d.ConstantColumn(72);
                                d.ConstantColumn(72);
                            });
                            t.Header(h =>
                            {
                                void HeadCell(string text, bool right = false)
                                {
                                    var cell = h.Cell().DefaultTextStyle(s => s.SemiBold()).PaddingVertical(4).BorderBottom(1);
                                    if (right) cell.AlignRight();
                                    cell.Text(text);
                                }
                                HeadCell("编码");
                                HeadCell("名称");
                                HeadCell("单位");
                                HeadCell("数量", true);
                                HeadCell("单价", true);
                                HeadCell("小计", true);
                            });
                            foreach (var it in order.Items)
                            {
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(it.Product?.Code ?? "");
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(it.Product?.Name ?? "");
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(it.Product?.Unit ?? "");
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text(it.Quantity.ToString());
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text(it.UnitPrice.ToString("0.00"));
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text((it.Quantity * it.UnitPrice).ToString("0.00"));
                            }
                        });
                        main.Item().PaddingTop(8).AlignRight().Text($"合计：¥ {order.TotalAmount:0.00}（大写：{MoneyChinese(order.TotalAmount)}）");
                    });
                    p.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"经手人：{HandlerName}");
                        row.AutoItem().Text(txt =>
                        {
                            txt.Span("打印：");
                            txt.Span($"{DateTime.Now:yyyy-MM-dd HH:mm}  ");
                            txt.CurrentPageNumber();
                            txt.Span(" / ");
                            txt.TotalPages();
                        });
                    });
                });
            }).GeneratePdf();

        public byte[] RenderSale(SaleOrder order) =>
            Document.Create(c =>
            {
                c.Page(p =>
                {
                    p.Margin(36);
                    p.DefaultTextStyle(t => t.FontFamily(Font).FontSize(10));
                    p.Header().Column(col =>
                    {
                        col.Item().Text(_opt.Company.Name).SemiBold().FontSize(14);
                        col.Item().Text($"销售单 {order.OrderNo}").SemiBold();
                        col.Item().Text($"日期：{order.OrderDate:yyyy-MM-dd}  状态：{order.Status}");
                        col.Item().Text($"客户：{order.Customer?.Name}");
                        col.Item().Text($"电话：{order.Customer?.Phone ?? "—"}  地址：{order.Customer?.Address ?? "—"}");
                    });
                    p.Content().Column(main =>
                    {
                        main.Item().Table(t =>
                        {
                            t.ColumnsDefinition(d =>
                            {
                                d.RelativeColumn(2);
                                d.RelativeColumn(3);
                                d.ConstantColumn(48);
                                d.ConstantColumn(72);
                                d.ConstantColumn(72);
                                d.ConstantColumn(72);
                            });
                            t.Header(h =>
                            {
                                void HeadCell(string text, bool right = false)
                                {
                                    var cell = h.Cell().DefaultTextStyle(s => s.SemiBold()).PaddingVertical(4).BorderBottom(1);
                                    if (right) cell.AlignRight();
                                    cell.Text(text);
                                }
                                HeadCell("编码");
                                HeadCell("名称");
                                HeadCell("单位");
                                HeadCell("数量", true);
                                HeadCell("单价", true);
                                HeadCell("小计", true);
                            });
                            foreach (var it in order.Items)
                            {
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(it.Product?.Code ?? "");
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(it.Product?.Name ?? "");
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(it.Product?.Unit ?? "");
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text(it.Quantity.ToString());
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text(it.UnitPrice.ToString("0.00"));
                                t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text((it.Quantity * it.UnitPrice).ToString("0.00"));
                            }
                        });
                        main.Item().PaddingTop(8).AlignRight().Text($"合计：¥ {order.TotalAmount:0.00}（大写：{MoneyChinese(order.TotalAmount)}）");
                    });
                    p.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"经手人：{HandlerName}");
                        row.AutoItem().Text(txt =>
                        {
                            txt.Span("打印：");
                            txt.Span($"{DateTime.Now:yyyy-MM-dd HH:mm}  ");
                            txt.CurrentPageNumber();
                            txt.Span(" / ");
                            txt.TotalPages();
                        });
                    });
                });
            }).GeneratePdf();

        public byte[] RenderStocktake(Stocktake st) =>
            Document.Create(c =>
            {
                c.Page(p =>
                {
                    p.Margin(36);
                    p.DefaultTextStyle(t => t.FontFamily(Font).FontSize(10));
                    p.Header().Column(col =>
                    {
                        col.Item().Text(_opt.Company.Name).SemiBold().FontSize(14);
                        col.Item().Text($"盘点单 {st.OrderNo}").SemiBold();
                        col.Item().Text($"日期：{st.OrderDate:yyyy-MM-dd}  状态：{st.Status}");
                    });
                    p.Content().Table(t =>
                    {
                        t.ColumnsDefinition(d =>
                        {
                            d.RelativeColumn(2);
                            d.RelativeColumn(3);
                            d.ConstantColumn(72);
                            d.ConstantColumn(72);
                            d.ConstantColumn(72);
                        });
                        t.Header(h =>
                        {
                            void HeadCell(string text, bool right = false)
                            {
                                var cell = h.Cell().DefaultTextStyle(s => s.SemiBold()).PaddingVertical(4).BorderBottom(1);
                                if (right) cell.AlignRight();
                                cell.Text(text);
                            }
                            HeadCell("编码");
                            HeadCell("名称");
                            HeadCell("系统", true);
                            HeadCell("实盘", true);
                            HeadCell("差异", true);
                        });
                        foreach (var it in st.Items)
                        {
                            t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(it.Product?.Code ?? "");
                            t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text(it.Product?.Name ?? "");
                            t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text(it.SystemQty.ToString());
                            t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text(it.ActualQty.ToString());
                            t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text((it.ActualQty - it.SystemQty).ToString());
                        }
                    });
                    p.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"经手人：{HandlerName}");
                        row.AutoItem().Text(txt =>
                        {
                            txt.Span("打印：");
                            txt.Span($"{DateTime.Now:yyyy-MM-dd HH:mm}  ");
                            txt.CurrentPageNumber();
                            txt.Span(" / ");
                            txt.TotalPages();
                        });
                    });
                });
            }).GeneratePdf();

        /// <summary>金额转中文大写（整数部分，分角简化）。</summary>
        private static string MoneyChinese(decimal amount)
        {
            var n = (long)Math.Floor(amount);
            if (n == 0) return "零元整";
            var digits = "零壹贰叁肆伍陆柒捌玖";
            var units = new[] { "", "拾", "佰", "仟", "万", "拾", "佰", "仟", "亿" };
            if (n >= 100_000_000_000L) return "金额过大";
            var s = n.ToString();
            var len = s.Length;
            var chars = new List<char>();
            for (var i = 0; i < len; i++)
            {
                var d = s[i] - '0';
                var u = len - 1 - i;
                if (d != 0)
                {
                    chars.Add(digits[d]);
                    chars.Add(units[u][0]);
                }
                else if (chars.Count > 0 && chars[^1] != '零')
                    chars.Add('零');
            }
            return new string(chars.ToArray()).TrimEnd('零') + "元整";
        }
    }
}
