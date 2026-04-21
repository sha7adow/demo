using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using 进销存demo.Models.Options;
using 进销存demo.Models.Queries;

namespace 进销存demo.Filters;

public sealed class PopulatePagingDefaultsFilter : IAsyncActionFilter
{
    private readonly PagingOptions _paging;

    public PopulatePagingDefaultsFilter(IOptions<JxcOptions> options) =>
        _paging = options.Value.Paging;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is IPagedQuery q)
            {
                if (q.Page < 1) q.Page = 1;
                if (q.PageSize <= 0) q.PageSize = _paging.DefaultPageSize;
                if (q.PageSize > _paging.MaxPageSize) q.PageSize = _paging.MaxPageSize;
            }
        }

        await next();
    }
}
