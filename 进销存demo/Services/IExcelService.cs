namespace 进销存demo.Services
{
    public interface IExcelService
    {
        byte[] Export<T>(IEnumerable<T> rows, string sheetName, IReadOnlyList<ColumnSpec<T>> cols);
        List<T> Import<T>(Stream stream, IReadOnlyList<ImportColumn<T>> cols) where T : new();
    }

    public record ColumnSpec<T>(string Header, Func<T, object?> Selector, string? Format = null);

    public record ImportColumn<T>(string Header, Action<T, string?> Setter, bool Required = false);
}
