namespace 进销存demo.Models.Entities
{
    public enum OrderStatus
    {
        Draft = 0,
        Confirmed = 1,
        Cancelled = 2,
        Returned = 3
    }

    public enum StockChangeType
    {
        Purchase = 1,
        Sale = 2,
        Adjust = 3,
        PurchaseReturn = 4,
        SaleReturn = 5,
        Stocktake = 6
    }

    public enum StocktakeStatus
    {
        Draft = 0,
        Confirmed = 1,
        Cancelled = 2
    }
}
