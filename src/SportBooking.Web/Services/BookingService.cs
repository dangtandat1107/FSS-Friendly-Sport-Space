using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

public record BookingResult(bool Success, string? Error = null, Booking? Booking = null);

public interface IBookingService
{
    const int OpenHour = 6;
    const int CloseHour = 22;
    const int PaymentTimeoutMinutes = 15;   // thời gian giữ chỗ chờ thanh toán
    Task<BookingResult> CreateAsync(string customerId, int courtId, DateTime start, int hours);
    // Trạng thái từng giờ của từng sân trong một ngày, dùng để vẽ lịch dạng lưới: grid[courtId][hour]
    Task<Dictionary<int, Dictionary<int, SlotState>>> GetDayGridAsync(IReadOnlyCollection<Court> courts, DateTime date);
}

public class BookingService : IBookingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<BookingService> _log;
    public BookingService(AppDbContext db, ILogger<BookingService> log) { _db = db; _log = log; }

    public async Task<BookingResult> CreateAsync(string customerId, int courtId, DateTime start, int hours)
    {
        if (hours < 1 || hours > 4) return new(false, "Thời lượng đặt phải từ 1 đến 4 giờ.");
        start = new DateTime(start.Year, start.Month, start.Day, start.Hour, 0, 0);
        var end = start.AddHours(hours);

        if (start < DateTime.Now) return new(false, "Không thể đặt giờ trong quá khứ.");
        if (start.Hour < IBookingService.OpenHour || end > start.Date.AddHours(IBookingService.CloseHour))
            return new(false, $"Sân mở cửa từ {IBookingService.OpenHour}h đến {IBookingService.CloseHour}h.");

        try
        {
            // Serializable: 2 người cùng đặt 1 giờ thì chỉ 1 người thành công
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var court = await _db.Courts.FindAsync(courtId);
            if (court == null) return new(false, "Sân không tồn tại.");
            if (court.Status != CourtStatus.Active) return new(false, "Sân đang bảo trì.");

            // Một lịch "đang giữ chỗ" nếu đã xác nhận, hoặc đang chờ thanh toán mà chưa quá hạn
            var now = DateTime.Now;
            var conflict = await _db.Bookings.AnyAsync(b =>
                b.CourtId == courtId && b.StartTime < end && b.EndTime > start &&
                (b.Status == BookingStatus.Confirmed ||
                 (b.Status == BookingStatus.Pending && (b.ExpiresAt == null || b.ExpiresAt > now))));
            if (conflict) return new(false, "Khung giờ này đã có người đặt.");

            var booking = new Booking
            {
                CourtId = courtId, CustomerId = customerId,
                StartTime = start, EndTime = end,
                TotalPrice = court.PricePerHour * hours,
                ExpiresAt = now.AddMinutes(IBookingService.PaymentTimeoutMinutes)
            };
            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            _log.LogInformation("Booking {Id} created: court {Court} {Start:u} x{Hours}h", booking.Id, courtId, start, hours);
            return new(true, null, booking);
        }
        catch (Exception ex) when (ex is DbUpdateException or SqlException)
        {
            _log.LogWarning(ex, "Booking failed due to DB contention");
            return new(false, "Hệ thống đang bận, vui lòng thử lại.");
        }
    }

    public async Task<Dictionary<int, Dictionary<int, SlotState>>> GetDayGridAsync(IReadOnlyCollection<Court> courts, DateTime date)
    {
        var d0 = date.Date; var d1 = d0.AddDays(1);
        var now = DateTime.Now;
        var ids = courts.Select(c => c.Id).ToList();

        // Cùng điều kiện "đang giữ chỗ" với CreateAsync: đã xác nhận, hoặc đang chờ thanh toán mà chưa quá hạn
        var list = await _db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.CourtId) && b.StartTime < d1 && b.EndTime > d0 &&
                (b.Status == BookingStatus.Confirmed ||
                 (b.Status == BookingStatus.Pending && (b.ExpiresAt == null || b.ExpiresAt > now))))
            .Select(b => new { b.CourtId, b.StartTime, b.EndTime, b.Status }).ToListAsync();

        var grid = new Dictionary<int, Dictionary<int, SlotState>>();
        foreach (var c in courts)
        {
            var row = new Dictionary<int, SlotState>();
            for (var h = IBookingService.OpenHour; h < IBookingService.CloseHour; h++)
                row[h] = c.Status != CourtStatus.Active ? SlotState.Closed
                       : d0.AddHours(h) < now ? SlotState.Past
                       : SlotState.Free;
            grid[c.Id] = row;
        }

        foreach (var b in list)
            for (var t = b.StartTime; t < b.EndTime; t = t.AddHours(1))
            {
                if (t.Date != d0) continue;
                var row = grid[b.CourtId];
                // Sân bảo trì giữ nguyên; ô đã xác nhận (đỏ) không bị ô "đang giữ" (vàng) ghi đè
                if (!row.TryGetValue(t.Hour, out var cur) || cur is SlotState.Closed or SlotState.Booked) continue;
                row[t.Hour] = b.Status == BookingStatus.Confirmed ? SlotState.Booked : SlotState.Held;
            }
        return grid;
    }
}
