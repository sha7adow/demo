namespace 进销存demo.Services
{
    /// <summary>
    /// 单据编号生成：按「前缀 + 当天日期」为 Scope 取流水，保证同一事务下不跳号、不重复。
    /// 生成格式：{prefix}{yyyyMMdd}-{0000}，如 PO20260420-0001。
    /// </summary>
    public interface IOrderNoGenerator
    {
        /// <summary>在当前 DbContext 上下文内取下一个单号（需在事务内调用以保证强一致性）。</summary>
        Task<string> NextAsync(string prefix, DateTime? date = null, CancellationToken ct = default);
    }
}
