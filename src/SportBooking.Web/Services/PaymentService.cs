using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

public record PaymentResult(bool Success, string? Error = null, Payment? Payment = null);

public interface IPaymentService
{
    /// <summary>Khách chọn phương thức: tạo một giao dịch chờ thanh toán.</summary>
    Task<PaymentResult> StartAsync(string customerId, int bookingId, PaymentMethod method);

    /// <summary>Kết quả từ cổng thanh toán giả lập: thành công hoặc thất bại.</summary>
    Task<PaymentResult> CompleteAsync(string customerId, int paymentId, bool success);
}

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PaymentService> _log;
    public PaymentService(AppDbContext db, ILogger<PaymentService> log) { _db = db; _log = log; }

    public async Task<PaymentResult> StartAsync(string customerId, int bookingId, PaymentMethod method)
    {
        var booking = await _db.Bookings.Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.CustomerId == customerId);
        if (booking == null) return new(false, "Không tìm thấy lịch đặt.");
        if (booking.Status != BookingStatus.Pending) return new(false, "Lịch này không ở trạng thái chờ thanh toán.");

        if (booking.IsExpired)
        {
            await ExpireAsync(booking);
            return new(false, "Đã quá hạn thanh toán, lịch đặt đã bị hủy.");
        }
        if (booking.Payments.Any(p => p.Status == PaymentStatus.Paid))
            return new(false, "Lịch này đã được thanh toán.");

        // Mỗi lần chọn lại phương thức thì giao dịch cũ chưa trả bị hủy
        foreach (var old in booking.Payments.Where(p => p.Status == PaymentStatus.Pending))
            old.Status = PaymentStatus.Cancelled;

        var payment = new Payment
        {
            BookingId = booking.Id,
            Amount = booking.TotalPrice,
            Method = method,
            Status = PaymentStatus.Pending,
            TransactionCode = $"FSS{booking.Id}{Random.Shared.Next(1000, 9999)}",
            CreatedAt = DateTime.Now
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();

        _log.LogInformation("Payment {Code} started for booking {Id} via {Method}", payment.TransactionCode, booking.Id, method);
        return new(true, null, payment);
    }

    public async Task<PaymentResult> CompleteAsync(string customerId, int paymentId, bool success)
    {
        var payment = await _db.Payments.Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Booking!.CustomerId == customerId);
        if (payment == null) return new(false, "Không tìm thấy giao dịch.");
        if (payment.Status != PaymentStatus.Pending) return new(false, "Giao dịch này đã được xử lý.");

        var booking = payment.Booking!;
        if (booking.Status != BookingStatus.Pending) return new(false, "Lịch đặt này không còn chờ thanh toán.");

        if (booking.IsExpired)
        {
            await ExpireAsync(booking);
            return new(false, "Đã quá hạn thanh toán, lịch đặt đã bị hủy.");
        }

        if (!success)
        {
            payment.Status = PaymentStatus.Failed;
            await _db.SaveChangesAsync();
            _log.LogInformation("Payment {Code} failed", payment.TransactionCode);
            return new(false, "Thanh toán không thành công. Bạn có thể thử lại trước khi hết hạn.", payment);
        }

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = DateTime.Now;
        booking.Status = BookingStatus.Confirmed;     // trả tiền xong thì lịch tự động được xác nhận
        await _db.SaveChangesAsync();

        _log.LogInformation("Payment {Code} paid, booking {Id} confirmed", payment.TransactionCode, booking.Id);
        return new(true, null, payment);
    }

    // Quá hạn: hủy lịch và các giao dịch đang chờ
    private async Task ExpireAsync(Booking booking)
    {
        booking.Status = BookingStatus.Cancelled;
        var pending = await _db.Payments
            .Where(p => p.BookingId == booking.Id && p.Status == PaymentStatus.Pending).ToListAsync();
        foreach (var p in pending) p.Status = PaymentStatus.Cancelled;
        await _db.SaveChangesAsync();
        _log.LogInformation("Booking {Id} hết hạn thanh toán, đã hủy", booking.Id);
    }
}
