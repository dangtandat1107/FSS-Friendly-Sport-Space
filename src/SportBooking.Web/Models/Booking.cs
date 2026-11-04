namespace SportBooking.Web.Models;

public class Booking
{
    public int Id { get; set; }
    public int CourtId { get; set; }
    public Court? Court { get; set; }
    public string CustomerId { get; set; } = "";
    public ApplicationUser? Customer { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Hạn thanh toán. Quá hạn mà chưa trả tiền thì lịch coi như hết hiệu lực
    public DateTime? ExpiresAt { get; set; }

    public List<Payment> Payments { get; set; } = new();

    // Chỉ có getter nên EF không tạo cột này trong database
    public bool IsExpired => Status == BookingStatus.Pending && ExpiresAt != null && ExpiresAt <= DateTime.Now;
}
