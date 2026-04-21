using ClosedXML.Excel;

namespace 进销存demo.Services
{
    public class ExcelService : IExcelService
    {
        public byte[] Export<T>(IEnumerable<T> rows, string sheetName, IReadOnlyList<ColumnSpec<T>> cols)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add(sheetName.Length > 31 ? sheetName[..31] : sheetName);

            // Header
            for (var c = 0; c < cols.Count; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = cols[c].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#f2f2f2");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            ws.SheetView.FreezeRows(1);

            // Column formats
            for (var c = 0; c < cols.Count; c++)
            {
                var fmt = cols[c].Format;
                if (!string.IsNullOrWhiteSpace(fmt))
                    ws.Column(c + 1).Style.NumberFormat.Format = fmt;
            }

            var r = 2;
            foreach (var row in rows)
            {
                for (var c = 0; c < cols.Count; c++)
                {
                    var v = cols[c].Selector(row);
                    var cell = ws.Cell(r, c + 1);
                    if (v == null)
                    {
                        cell.Value = string.Empty;
                        continue;
                    }

                    // Preserve types so Excel can sort/filter as numbers/dates.
                    switch (v)
                    {
                        case DateTime dt:
                            cell.Value = dt;
                            break;
                        case DateTimeOffset dto:
                            cell.Value = dto.DateTime;
                            break;
                        case bool b:
                            cell.Value = b ? 1 : 0;
                            break;
                        case byte n:
                            cell.Value = n;
                            break;
                        case short n:
                            cell.Value = n;
                            break;
                        case int n:
                            cell.Value = n;
                            break;
                        case long n:
                            cell.Value = n;
                            break;
                        case float n:
                            cell.Value = n;
                            break;
                        case double n:
                            cell.Value = n;
                            break;
                        case decimal n:
                            cell.Value = n;
                            break;
                        default:
                            cell.Value = v.ToString() ?? string.Empty;
                            break;
                    }
                }
                r++;
            }
            var used = ws.RangeUsed();
            if (used != null)
            {
                used.SetAutoFilter();
                used.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Columns(1, cols.Count).AdjustToContents(1, Math.Min(r, 200)); // avoid extreme cost on huge sheets
            }
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public List<T> Import<T>(Stream stream, IReadOnlyList<ImportColumn<T>> cols) where T : new()
        {
            using var wb = new XLWorkbook(stream);
            var ws = wb.Worksheet(1);
            var first = ws.FirstRowUsed();
            var last = ws.LastRowUsed();
            if (first == null || last == null) return new List<T>();

            var headerRow = first.RowNumber();
            var lastRow = last.RowNumber();
            var maxCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in ws.Row(headerRow).CellsUsed())
                colMap[cell.GetString().Trim()] = cell.Address.ColumnNumber;

            var result = new List<T>();
            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                var rowText = string.Join("", Enumerable.Range(1, maxCol).Select(c => ws.Cell(r, c).GetString()));
                if (string.IsNullOrWhiteSpace(rowText)) continue;

                var item = new T();
                foreach (var col in cols)
                {
                    if (!colMap.TryGetValue(col.Header, out var colNo))
                    {
                        if (col.Required)
                            throw new InvalidOperationException($"缺少列：{col.Header}");
                        continue;
                    }
                    var raw = ws.Cell(r, colNo).GetString().Trim();
                    if (string.IsNullOrEmpty(raw) && col.Required)
                        throw new InvalidOperationException($"第 {r} 行「{col.Header}」不能为空");
                    col.Setter(item, string.IsNullOrEmpty(raw) ? null : raw);
                }
                result.Add(item);
            }

            return result;
        }
    }
}
