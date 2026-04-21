using Microsoft.EntityFrameworkCore;
using 进销存demo.Models.Options;

namespace 进销存demo.Models
{
    /// <summary>
    /// 简易分页结果。Items 为当前页数据，其余字段用于渲染分页条。
    /// </summary>
    public class PagedList<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int PageIndex { get; }   // 从 1 开始
        public int PageSize { get; }
        public int TotalCount { get; }
        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPrevious => PageIndex > 1;
        public bool HasNext => PageIndex < TotalPages;

        public PagedList(IReadOnlyList<T> items, int totalCount, int pageIndex, int pageSize)
        {
            Items = items;
            TotalCount = totalCount;
            PageIndex = pageIndex;
            PageSize = pageSize;
        }

        public static async Task<PagedList<T>> CreateAsync(
            IQueryable<T> source,
            int pageIndex,
            int pageSize,
            PagingOptions paging,
            CancellationToken ct = default)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = paging.DefaultPageSize;
            if (pageSize > paging.MaxPageSize) pageSize = paging.MaxPageSize;

            var total = await source.CountAsync(ct);
            var items = await source.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new PagedList<T>(items, total, pageIndex, pageSize);
        }
    }
}
