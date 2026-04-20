namespace 进销存demo.Models.Entities
{
    /// <summary>
    /// 支持软删除的实体。DbContext 会对此类实体注册全局查询过滤器。
    /// </summary>
    public interface ISoftDelete
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
    }

    /// <summary>
    /// 审计字段。CreatedAt 由实体默认赋值；UpdatedAt 由 SaveChanges 拦截器在变更时写入。
    /// </summary>
    public interface IAuditable
    {
        DateTime CreatedAt { get; set; }
        DateTime? UpdatedAt { get; set; }
    }
}
