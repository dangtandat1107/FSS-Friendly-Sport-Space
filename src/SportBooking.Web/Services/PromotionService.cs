using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

public record OrderLine(int ProductId, int Quantity);
public record PricedLine(int ProductId, string Name, ProductCategory Category, int Quantity, decimal UnitPrice, decimal LineTotal, bool IsFree);
public record PromoBanner(string Kind, string Text);   // Kind: bulk hoặc streak

// Kết quả tính đơn nước: các dòng (gồm cả quà tặng), tiền nước, thông báo khuyến mãi và lỗi (nếu có)
public record DrinkQuote(bool Ok, List<string> Errors, List<PricedLine> Lines, decimal DrinksTotal, List<PromoBanner> Banners);

public interface IPromotionService
{
    /// <summary>
    /// Kiểm tra đơn nước (giới hạn số lượng, tồn kho) và tự áp dụng khuyến mãi đang bật.
    /// Dùng chung cho xem trước trong hộp thoại và cho lúc tạo đơn thật, nên khách thấy gì thì server tính đúng như vậy.
    /// streakDays: số ngày trong chuỗi tập luyện hiện tại của khách (lấy từ IStreakService.GetStatsAsync).
    /// </summary>
    Task<DrinkQuote> QuoteAsync(int courtId, IReadOnlyList<OrderLine> lines, int streakDays);
}

public class PromotionService : IPromotionService
{
    private readonly AppDbContext _db;
    private readonly IProductService _products;
    public PromotionService(AppDbContext db, IProductService products) { _db = db; _products = products; }

    public async Task<DrinkQuote> QuoteAsync(int courtId, IReadOnlyList<OrderLine> lines, int streakDays)
    {
        var errors = new List<string>();

        // Gộp các dòng trùng sản phẩm, bỏ dòng số lượng 0 hoặc âm
        var wanted = lines.Where(l => l.Quantity > 0)
            .GroupBy(l => l.ProductId).Select(g => new OrderLine(g.Key, g.Sum(x => x.Quantity))).ToList();
        if (wanted.Count > DrinkPolicy.MaxLines) errors.Add($"Một đơn tối đa {DrinkPolicy.MaxLines} loại sản phẩm.");

        var stock = (await _products.GetAvailableAsync(courtId)).ToDictionary(s => s.Product.Id);
        var remaining = stock.ToDictionary(kv => kv.Key, kv => kv.Value.Available);   // còn lại sau khi trừ phần khách đặt và quà tặng

        var paid = new List<PricedLine>();
        foreach (var w in wanted)
        {
            if (!stock.TryGetValue(w.ProductId, out var s)) { errors.Add("Có sản phẩm không còn bán ở sân này."); continue; }
            var p = s.Product;
            if (w.Quantity > DrinkPolicy.MaxPerProduct) { errors.Add($"{p.Name}: tối đa {DrinkPolicy.MaxPerProduct} mỗi đơn."); continue; }
            if (w.Quantity > remaining[p.Id])
            {
                errors.Add(remaining[p.Id] <= 0 ? $"{p.Name} đã hết hàng." : $"{p.Name} chỉ còn {remaining[p.Id]}.");
                continue;
            }
            remaining[p.Id] -= w.Quantity;
            paid.Add(new PricedLine(p.Id, p.Name, p.Category, w.Quantity, p.Price, p.Price * w.Quantity, false));
        }

        var gifts = new List<PricedLine>();
        var banners = new List<PromoBanner>();
        if (errors.Count == 0)
        {
            var promos = await _db.Promotions.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Id).ToListAsync();
            foreach (var promo in promos)
            {
                if (promo.Reward <= 0) continue;
                Product? giftProduct = null;
                string? text = null;

                if (promo.Type == PromotionType.BulkQuantityGift)
                {
                    // Đặt từ Condition chai trở lên thì tặng Reward chai. Tặng loại rẻ nhất trong đơn để chủ sân ít thiệt nhất
                    var bottles = paid.Where(l => DrinkPolicy.IsBottle(l.Category)).Sum(l => l.Quantity);
                    if (bottles >= promo.Condition)
                    {
                        giftProduct = paid.Where(l => DrinkPolicy.IsBottle(l.Category) && remaining[l.ProductId] >= promo.Reward)
                            .OrderBy(l => l.UnitPrice).Select(l => stock[l.ProductId].Product).FirstOrDefault();
                        text = $"🎉 Bạn được tặng {promo.Reward} chai miễn phí vì đặt từ {promo.Condition} chai!";
                    }
                }
                else if (promo.Type == PromotionType.StreakFreeWater)
                {
                    // Chuỗi tập luyện từ Condition ngày trở lên thì được free nước suối cho mỗi lần đặt sân
                    if (streakDays >= promo.Condition)
                    {
                        giftProduct = stock.Values.Where(s => s.Product.Category == ProductCategory.Water && remaining[s.Product.Id] >= promo.Reward)
                            .OrderBy(s => s.Product.Price).Select(s => s.Product).FirstOrDefault();
                        text = $"🔥 Free {promo.Reward} nước suối vì chuỗi tập luyện của bạn đã đạt {streakDays} ngày!";
                    }
                }

                // Hết hàng để tặng thì bỏ qua khuyến mãi này, không báo khống
                if (giftProduct == null || text == null) continue;
                remaining[giftProduct.Id] -= promo.Reward;
                gifts.Add(new PricedLine(giftProduct.Id, giftProduct.Name, giftProduct.Category, promo.Reward, giftProduct.Price, 0, true));
                banners.Add(new PromoBanner(promo.Type == PromotionType.BulkQuantityGift ? "bulk" : "streak", text));
            }
        }

        var all = paid.Concat(gifts).ToList();
        return new DrinkQuote(errors.Count == 0, errors, all, paid.Sum(l => l.LineTotal), banners);
    }
}
