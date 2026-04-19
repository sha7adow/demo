namespace 进销存demo.Models.Entities
{
    public enum OrderStatus
    {
        Draft = 0,
        Confirmed = 1,
        Cancelled = 2
    }

    public enum StockChangeType
    {
        Purchase = 1,
        Sale = 2,
        Adjust = 3
    }
}
