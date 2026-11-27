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
    public decimal TotalPrice { get; set; }                 // tiền sân (không gồm nước), giữ nguyên ý nghĩa như trước
    public decimal DrinksTotal { get; set; }                // tiền nước kèm theo (không tính quà tặng)
    public DrinkOrderStatus DrinkStatus { get; set; } = DrinkOrderStatus.None;
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Hạn thanh toán. Quá hạn mà chưa trả tiền thì lịch coi như hết hiệu lực
    public DateTime? ExpiresAt { get; set; }

    public List<Payment> Payments { get; set; } = new();
    public Review? Review { get; set; }   // đánh giá của khách cho lịch này (nếu có)
    public List<BookingItem> Items { get; set; } = new();   // đơn nước kèm theo (rỗng nếu không đặt)

    // Mã đơn hiển thị cho khách, ví dụ FSS-00042. Suy ra từ Id nên không cần cột riêng trong database
    public string Code => $"FSS-{Id:D5}";

    // Tổng khách phải trả = tiền sân + tiền nước. Số tiền thanh toán và mã VietQR lấy theo số này
    public decimal GrandTotal => TotalPrice + DrinksTotal;

    // Chỉ có getter nên EF không tạo cột này trong database
    public bool IsExpired => Status == BookingStatus.Pending && ExpiresAt != null && ExpiresAt <= DateTime.Now;
}
