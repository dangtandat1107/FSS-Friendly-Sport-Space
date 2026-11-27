using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Extensions;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

public interface IDrinkOrderService
{
    /// <summary>Gọi sau khi lịch được xác nhận: trừ kho thật và chuyển đơn nước sang "Chờ chuẩn bị". Lỗi chỉ ghi log, không làm hỏng việc xác nhận.</summary>
    Task OnConfirmedAsync(Booking booking);

    /// <summary>Gọi sau khi lịch bị hủy hoặc quá hạn: hoàn kho nếu chưa chuẩn bị, đơn nước chuyển sang "Đã hủy".</summary>
    Task OnCancelledAsync(Booking booking);

    /// <summary>Nhân viên hoặc chủ sân cập nhật Đã chuẩn bị / Đã giao cho đơn nước của sân mình phụ trách.</summary>
    Task<(bool Ok, string? Error)> SetStatusAsync(string userId, string role, int bookingId, DrinkOrderStatus to);

    /// <summary>Danh sách mã sân mà người này được xử lý đơn nước. null nghĩa là tất cả sân (Admin).</summary>
    Task<List<int>?> VisibleCourtIdsAsync(string userId, string role);

    /// <summary>Quá giờ chơi mà đơn nước chưa giao thì chuyển sang "Không nhận hàng". Trả về số đơn đã chuyển.</summary>
    Task<int> MarkNoShowAsync();
}

public class DrinkOrderService : IDrinkOrderService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DrinkOrderService> _log;
    public DrinkOrderService(AppDbContext db, ILogger<DrinkOrderService> log) { _db = db; _log = log; }

    public async Task OnConfirmedAsync(Booking booking)
    {
        try
        {
            // Chỉ làm một lần: đơn đang "Chờ thanh toán" mới trừ kho. Gọi lại nhiều lần cũng an toàn
            if (booking.DrinkStatus != DrinkOrderStatus.Awaiting) return;

            var lines = await _db.BookingItems.Where(i => i.BookingId == booking.Id).ToListAsync();
            var ids = lines.Select(l => l.ProductId).Distinct().ToList();
            var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            foreach (var l in lines)
                if (products.TryGetValue(l.ProductId, out var p)) p.Stock = Math.Max(0, p.Stock - l.Quantity);   // gồm cả quà tặng

            booking.DrinkStatus = DrinkOrderStatus.ToPrepare;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) { _log.LogError(ex, "Không cập nhật được đơn nước của lịch {Id} khi xác nhận", booking.Id); }
    }

    public async Task OnCancelledAsync(Booking booking)
    {
        try
        {
            if (booking.DrinkStatus is DrinkOrderStatus.None or DrinkOrderStatus.Cancelled
                or DrinkOrderStatus.Delivered or DrinkOrderStatus.NoShow) return;

            // Chỉ hoàn kho khi hàng chưa được chuẩn bị. Hàng đã chuẩn bị rồi thì coi như đã dùng.
            // Đơn đang "Chờ thanh toán" chưa bị trừ kho (chỉ được giữ chỗ) nên không cần hoàn.
            if (booking.DrinkStatus == DrinkOrderStatus.ToPrepare)
            {
                var lines = await _db.BookingItems.Where(i => i.BookingId == booking.Id).ToListAsync();
                var ids = lines.Select(l => l.ProductId).Distinct().ToList();
                var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
                foreach (var l in lines)
                    if (products.TryGetValue(l.ProductId, out var p)) p.Stock += l.Quantity;
            }
            booking.DrinkStatus = DrinkOrderStatus.Cancelled;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex) { _log.LogError(ex, "Không cập nhật được đơn nước của lịch {Id} khi hủy", booking.Id); }
    }

    public async Task<List<int>?> VisibleCourtIdsAsync(string userId, string role) => role switch
    {
        "Admin" => null,
        "Owner" => await _db.Courts.Where(c => c.OwnerId == userId).Select(c => c.Id).ToListAsync(),
        "Staff" => await _db.Staffs.Where(s => s.UserId == userId && s.IsActive).Select(s => s.CourtId).ToListAsync(),
        _ => new List<int>()
    };

    public async Task<(bool Ok, string? Error)> SetStatusAsync(string userId, string role, int bookingId, DrinkOrderStatus to)
    {
        var courtIds = await VisibleCourtIdsAsync(userId, role);
        var q = _db.Bookings.Where(x => x.Id == bookingId);
        if (courtIds != null) q = q.Where(x => courtIds.Contains(x.CourtId));   // null nghĩa là Admin, xem tất cả sân
        var b = await q.FirstOrDefaultAsync();
        if (b == null) return (false, "Không tìm thấy đơn nước hoặc bạn không phụ trách sân này.");
        if (b.Status != BookingStatus.Confirmed) return (false, "Chỉ xử lý đơn nước của lịch đã xác nhận thanh toán.");

        // ToPrepare → Prepared → Delivered. Cho phép giao thẳng từ ToPrepare khi nhân viên làm gọn một bước
        var ok = (b.DrinkStatus, to) switch
        {
            (DrinkOrderStatus.ToPrepare, DrinkOrderStatus.Prepared) => true,
            (DrinkOrderStatus.ToPrepare, DrinkOrderStatus.Delivered) => true,
            (DrinkOrderStatus.Prepared, DrinkOrderStatus.Delivered) => true,
            _ => false
        };
        if (!ok) return (false, $"Không thể chuyển từ \"{b.DrinkStatus.DisplayName()}\" sang \"{to.DisplayName()}\".");

        b.DrinkStatus = to;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<int> MarkNoShowAsync()
    {
        var limit = DateTime.Now.AddHours(-DrinkPolicy.NoShowAfterHours);
        var late = await _db.Bookings
            .Where(b => b.Status == BookingStatus.Confirmed && b.EndTime < limit
                     && (b.DrinkStatus == DrinkOrderStatus.ToPrepare || b.DrinkStatus == DrinkOrderStatus.Prepared))
            .ToListAsync();
        foreach (var b in late) b.DrinkStatus = DrinkOrderStatus.NoShow;
        if (late.Count > 0) await _db.SaveChangesAsync();
        return late.Count;
    }
}

// Tiến trình nền: mỗi 5 phút đánh dấu các đơn nước khách không tới nhận
public class DrinkNoShowWorker : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DrinkNoShowWorker> _log;
    public DrinkNoShowWorker(IServiceScopeFactory scopes, ILogger<DrinkNoShowWorker> log) { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var n = await scope.ServiceProvider.GetRequiredService<IDrinkOrderService>().MarkNoShowAsync();
                    if (n > 0) _log.LogInformation("Đã đánh dấu {Count} đơn nước không nhận hàng", n);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _log.LogError(ex, "Lỗi khi quét đơn nước không nhận hàng");
                }
            }
        }
        catch (OperationCanceledException) { /* ứng dụng đang tắt */ }
    }
}
