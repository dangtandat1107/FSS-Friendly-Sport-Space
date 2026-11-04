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
    Task<HashSet<int>> GetBusyHoursAsync(int courtId, DateTime date);
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

    public async Task<HashSet<int>> GetBusyHoursAsync(int courtId, DateTime date)
    {
        var d0 = date.Date; var d1 = d0.AddDays(1);
        var now = DateTime.Now;
        var list = await _db.Bookings
            .Where(b => b.CourtId == courtId && b.StartTime < d1 && b.EndTime > d0 &&
                (b.Status == BookingStatus.Confirmed ||
                 (b.Status == BookingStatus.Pending && (b.ExpiresAt == null || b.ExpiresAt > now))))
            .Select(b => new { b.StartTime, b.EndTime }).ToListAsync();

        var busy = new HashSet<int>();
        foreach (var b in list)
            for (var h = b.StartTime; h < b.EndTime; h = h.AddHours(1))
                if (h.Date == d0) busy.Add(h.Hour);
        return busy;
    }
}
