using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

public record PaymentResult(bool Success, string? Error = null, Payment? Payment = null);

public interface IPaymentService
{
    /// <summary>Khách chọn phương thức: tạo giao dịch chờ thanh toán (hoặc trả về giao dịch đang chờ xác nhận).</summary>
    Task<PaymentResult> StartAsync(string customerId, int bookingId, PaymentMethod method);

    /// <summary>Khách bấm "Tôi đã chuyển khoản": giao dịch chuyển sang chờ chủ sân xác nhận.</summary>
    Task<PaymentResult> ReportTransferAsync(string customerId, int paymentId);

    /// <summary>Chủ sân (hoặc Admin) xác nhận đã nhận tiền: giao dịch thành "Đã thanh toán", lịch thành "Đã xác nhận".</summary>
    Task<PaymentResult> ConfirmAsync(string staffId, bool isAdmin, int paymentId);

    /// <summary>Chủ sân báo chưa nhận được tiền: giao dịch thất bại, khách có thêm ít phút để thử lại.</summary>
    Task<PaymentResult> RejectAsync(string staffId, bool isAdmin, int paymentId);
}

public class PaymentService : IPaymentService
{
    public const int OwnerConfirmWindowMinutes = 120;   // thời gian chủ sân có để xác nhận sau khi khách báo chuyển khoản
    public const int RetryWindowMinutes = 15;           // thời gian khách có thêm để thử lại sau khi bị báo chưa nhận được tiền

    private readonly AppDbContext _db;
    private readonly ILogger<PaymentService> _log;
    public PaymentService(AppDbContext db, ILogger<PaymentService> log) { _db = db; _log = log; }

    public async Task<PaymentResult> StartAsync(string customerId, int bookingId, PaymentMethod method)
    {
        var booking = await _db.Bookings.Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.CustomerId == customerId);
        if (booking == null) return new(false, "Không tìm thấy lịch đặt.");
        if (booking.Status != BookingStatus.Pending) return new(false, "Lịch này không ở trạng thái chờ thanh toán.");
        if (booking.Payments.Any(p => p.Status == PaymentStatus.Paid))
            return new(false, "Lịch này đã được thanh toán.");

        // Đã báo chuyển khoản rồi thì quay lại màn hình chờ xác nhận, không tạo giao dịch mới
        var awaiting = booking.Payments.FirstOrDefault(p => p.Status == PaymentStatus.AwaitingConfirmation);
        if (awaiting != null) return new(true, null, awaiting);

        if (booking.IsExpired)
        {
            await ExpireAsync(booking);
            return new(false, "Đã quá hạn thanh toán, lịch đặt đã bị hủy.");
        }

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

    public async Task<PaymentResult> ReportTransferAsync(string customerId, int paymentId)
    {
        var payment = await _db.Payments.Include(p => p.Booking)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.Booking!.CustomerId == customerId);
        if (payment == null) return new(false, "Không tìm thấy giao dịch.");
        if (payment.Status == PaymentStatus.AwaitingConfirmation) return new(true, null, payment);   // bấm lặp lại thì bỏ qua
        if (payment.Status != PaymentStatus.Pending) return new(false, "Giao dịch này đã được xử lý.");

        var booking = payment.Booking!;
        if (booking.Status != BookingStatus.Pending) return new(false, "Lịch đặt này không còn chờ thanh toán.");
        if (booking.IsExpired)
        {
            await ExpireAsync(booking);
            return new(false, "Đã quá hạn thanh toán, lịch đặt đã bị hủy.");
        }

        payment.Status = PaymentStatus.AwaitingConfirmation;
        // Kéo dài thời gian giữ chỗ để chủ sân kịp kiểm tra, nhưng không vượt quá giờ bắt đầu chơi
        var until = DateTime.Now.AddMinutes(OwnerConfirmWindowMinutes);
        booking.ExpiresAt = until < booking.StartTime ? until : booking.StartTime;
        await _db.SaveChangesAsync();

        _log.LogInformation("Payment {Code} reported by customer, awaiting owner confirmation", payment.TransactionCode);
        return new(true, null, payment);
    }

    public async Task<PaymentResult> ConfirmAsync(string staffId, bool isAdmin, int paymentId)
    {
        var payment = await LoadForStaffAsync(staffId, isAdmin, paymentId);
        if (payment == null) return new(false, "Không tìm thấy giao dịch.");
        if (payment.Status != PaymentStatus.AwaitingConfirmation)
            return new(false, "Giao dịch này không ở trạng thái chờ xác nhận.");

        var booking = payment.Booking!;
        if (booking.Status == BookingStatus.Cancelled) return new(false, "Lịch đặt đã bị hủy, hãy hoàn tiền cho khách.");

        // Quá hạn giữ chỗ: chỉ xác nhận được nếu khung giờ đó chưa có người khác đặt
        if (booking.IsExpired)
        {
            var now = DateTime.Now;
            var taken = await _db.Bookings.AnyAsync(b =>
                b.Id != booking.Id && b.CourtId == booking.CourtId &&
                b.StartTime < booking.EndTime && b.EndTime > booking.StartTime &&
                (b.Status == BookingStatus.Confirmed ||
                 (b.Status == BookingStatus.Pending && (b.ExpiresAt == null || b.ExpiresAt > now))));
            if (taken)
                return new(false, "Khung giờ này đã có người khác đặt sau khi hết hạn giữ chỗ. Hãy hoàn tiền cho khách.");
        }

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = DateTime.Now;
        booking.Status = BookingStatus.Confirmed;
        await _db.SaveChangesAsync();

        _log.LogInformation("Payment {Code} confirmed by staff {Staff}", payment.TransactionCode, staffId);
        return new(true, null, payment);
    }

    public async Task<PaymentResult> RejectAsync(string staffId, bool isAdmin, int paymentId)
    {
        var payment = await LoadForStaffAsync(staffId, isAdmin, paymentId);
        if (payment == null) return new(false, "Không tìm thấy giao dịch.");
        if (payment.Status != PaymentStatus.AwaitingConfirmation)
            return new(false, "Giao dịch này không ở trạng thái chờ xác nhận.");

        payment.Status = PaymentStatus.Failed;
        var booking = payment.Booking!;
        if (booking.Status == BookingStatus.Pending)
        {
            var until = DateTime.Now.AddMinutes(RetryWindowMinutes);
            booking.ExpiresAt = until < booking.StartTime ? until : booking.StartTime;
        }
        await _db.SaveChangesAsync();

        _log.LogInformation("Payment {Code} rejected by staff {Staff}", payment.TransactionCode, staffId);
        return new(true, null, payment);
    }

    // Chủ sân chỉ thao tác được với giao dịch của sân mình; Admin thao tác được tất cả
    private Task<Payment?> LoadForStaffAsync(string staffId, bool isAdmin, int paymentId) =>
        _db.Payments.Include(p => p.Booking).ThenInclude(b => b!.Court)
            .FirstOrDefaultAsync(p => p.Id == paymentId && (isAdmin || p.Booking!.Court!.OwnerId == staffId));

    // Quá hạn: hủy lịch và các giao dịch đang chờ thanh toán
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
