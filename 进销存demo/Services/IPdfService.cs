using 进销存demo.Models.Entities;

namespace 进销存demo.Services
{
    public interface IPdfService
    {
        byte[] RenderPurchase(PurchaseOrder order);
        byte[] RenderSale(SaleOrder order);
        byte[] RenderStocktake(Stocktake st);
    }
}
