namespace 进销存demo.Models.Queries;

/// <summary>列表查询分页参数；由 <see cref="Filters.PopulatePagingDefaultsFilter"/> 在 Action 执行前规范化。</summary>
public interface IPagedQuery
{
    int Page { get; set; }
    int PageSize { get; set; }
}
