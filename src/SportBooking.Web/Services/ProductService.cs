using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

// Các con số nghiệp vụ của đơn nước. Chỉnh ở đây một chỗ
public static class DrinkPolicy
{
    public const int MaxPerProduct = 10;     // tối đa mỗi sản phẩm trong một đơn (chống đặt số lượng bất thường)
    public const int MaxLines = 8;           // tối đa số loại sản phẩm khác nhau trong một đơn
    public const int NoShowAfterHours = 2;   // sau giờ kết thúc buổi chơi bao lâu mà chưa giao thì đánh dấu "Không nhận hàng"

    // Các loại tính là "chai" khi xét khuyến mãi mua nhiều (khăn lạnh và phụ kiện không tính)
    public static bool IsBottle(ProductCategory c) => c is ProductCategory.Water or ProductCategory.Electrolyte or ProductCategory.SoftDrink;

    // Biểu tượng dùng khi sản phẩm chưa có ảnh
    public static string Icon(ProductCategory c) => c switch
    {
        ProductCategory.Water => "💧",
        ProductCategory.Electrolyte => "⚡",
        ProductCategory.SoftDrink => "🥤",
        ProductCategory.Towel => "🧻",
        _ => "🎽"
    };
}

public record ProductStock(Product Product, int Available);

public interface IProductService
{
    /// <summary>Sản phẩm đang bán của một sân, kèm số lượng còn có thể đặt (kho trừ số đang được giữ cho đơn chờ thanh toán).</summary>
    Task<List<ProductStock>> GetAvailableAsync(int courtId);
}

public class ProductService : IProductService
{
    private readonly AppDbContext _db;
    public ProductService(AppDbContext db) { _db = db; }

    public async Task<List<ProductStock>> GetAvailableAsync(int courtId)
    {
        var products = await _db.Products.AsNoTracking()
            .Where(p => p.CourtId == courtId && p.IsActive)
            .OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
        if (products.Count == 0) return new();

        // Hàng đang được giữ: đơn đang chờ thanh toán và chưa quá hạn. Đơn đã xác nhận thì kho đã được trừ thật (DrinkOrderService),
        // đơn hủy hoặc quá hạn thì tự hết giữ. Nhờ tính như vậy nên không cần trả hàng về kho khi đơn quá hạn.
        var now = DateTime.Now;
        var ids = products.Select(p => p.Id).ToList();
        var reserved = await _db.BookingItems.AsNoTracking()
            .Where(i => ids.Contains(i.ProductId)
                     && i.Booking!.Status == BookingStatus.Pending
                     && i.Booking.DrinkStatus == DrinkOrderStatus.Awaiting
                     && (i.Booking.ExpiresAt == null || i.Booking.ExpiresAt > now))
            .GroupBy(i => i.ProductId)
            .Select(g => new { g.Key, Qty = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.Key, x => x.Qty);

        return products.Select(p => new ProductStock(p, Math.Max(0, p.Stock - reserved.GetValueOrDefault(p.Id)))).ToList();
    }
}
