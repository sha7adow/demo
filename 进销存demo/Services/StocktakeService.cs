using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using 进销存demo.Data;
using 进销存demo.Models.Entities;
using 进销存demo.Models.Options;

namespace 进销存demo.Services
{
    public class StocktakeService : IStocktakeService
    {
        private readonly AppDbContext _db;
        private readonly IInventoryService _inventory;
        private readonly IOrderNoGenerator _orderNo;
        private readonly string _prefix;

        public StocktakeService(AppDbContext db, IInventoryService inventory, IOrderNoGenerator orderNo, IOptions<JxcOptions> options)
        {
            _db = db;
            _inventory = inventory;
            _orderNo = orderNo;
            _prefix = options.Value.OrderPrefix.Stocktake;
        }

        public async Task<string> GenerateOrderNoAsync()
        {
            var day = DateTime.Today.ToString("yyyyMMdd");
            var scope = $"{_prefix}-{day}";
            var seq = await _db.SequenceNumbers.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Scope == scope);
            var preview = seq?.NextValue ?? 1;
            return $"{_prefix}{day}-{preview:D4}";
        }

        public async Task<List<Stocktake>> ListAsync() =>
            await _db.Stocktakes.OrderByDescending(s => s.Id).ToListAsync();

        public async Task<Stocktake?> GetAsync(int id) =>
            await _db.Stocktakes
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

        public async Task<Stocktake> CreateAsync(string? remark)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();

            var products = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Code).ToListAsync();

            var st = new Stocktake
            {
                OrderNo = await _orderNo.NextAsync(_prefix),
                OrderDate = DateTime.Today,
                Status = StocktakeStatus.Draft,
                Remark = remark,
                CreatedAt = DateTime.Now,
                Items = products.Select(p => new StocktakeItem
                {
                    ProductId = p.Id,
                    SystemQty = p.Stock,
                    ActualQty = p.Stock
                }).ToList()
            };

            _db.Stocktakes.Add(st);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return st;
        }

        public async Task UpdateActualQtyAsync(int stocktakeId, Dictionary<int, int> actualQtyByProductId)
        {
            var st = await _db.Stocktakes
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == stocktakeId)
                ?? throw new InvalidOperationException("盘点单不存在");

            if (st.Status != StocktakeStatus.Draft)
                throw new InvalidOperationException("仅草稿状态的盘点单可以修改实盘数");

            foreach (var item in st.Items)
            {
                if (actualQtyByProductId.TryGetValue(item.ProductId, out var qty))
                {
                    if (qty < 0) throw new InvalidOperationException($"实盘数不能为负数（ProductId={item.ProductId}）");
                    item.ActualQty = qty;
                }
            }

            await _db.SaveChangesAsync();
        }

        public async Task ConfirmAsync(int id)
        {
            await using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var st = await _db.Stocktakes
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == id)
                    ?? throw new InvalidOperationException("盘点单不存在");

                if (st.Status != StocktakeStatus.Draft)
                    throw new InvalidOperationException("仅草稿状态的盘点单可以确认");

                foreach (var item in st.Items)
                {
                    var diff = item.ActualQty - item.SystemQty;
                    if (diff == 0) continue;

                    await _inventory.ApplyStockChangeAsync(
                        item.ProductId,
                        diff,
                        StockChangeType.Stocktake,
                        st.OrderNo,
                        $"盘点 #{st.OrderNo}：系统 {item.SystemQty}，实盘 {item.ActualQty}");
                }

                st.Status = StocktakeStatus.Confirmed;
                st.ConfirmedAt = DateTime.Now;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync();
                throw new InvalidOperationException("相关商品已被其他操作修改，请刷新后重试");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task CancelAsync(int id)
        {
            var st = await _db.Stocktakes.FirstOrDefaultAsync(s => s.Id == id)
                ?? throw new InvalidOperationException("盘点单不存在");

            if (st.Status == StocktakeStatus.Confirmed)
                throw new InvalidOperationException("已确认的盘点单不能取消");

            st.Status = StocktakeStatus.Cancelled;
            await _db.SaveChangesAsync();
        }
    }
}
