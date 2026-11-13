using System.ComponentModel.DataAnnotations;
namespace SportBooking.Web.Models;

public enum SportType
{
    [Display(Name = "Cầu lông")] Badminton,
    [Display(Name = "Bóng đá")] Football,
    [Display(Name = "Tennis")] Tennis,
    [Display(Name = "Pickleball")] Pickleball,
    [Display(Name = "Bóng rổ")] Basketball
}

public enum CourtStatus
{
    [Display(Name = "Hoạt động")] Active,
    [Display(Name = "Bảo trì")] Maintenance
}

public enum BookingStatus
{
    [Display(Name = "Chờ thanh toán")] Pending,
    [Display(Name = "Đã xác nhận")] Confirmed,
    [Display(Name = "Đã hủy")] Cancelled
}

public enum PaymentMethod
{
    [Display(Name = "Chuyển khoản ngân hàng")] BankTransfer,
    [Display(Name = "Ví MoMo")] Momo
}

public enum PaymentStatus
{
    [Display(Name = "Chờ thanh toán")] Pending,
    [Display(Name = "Đã thanh toán")] Paid,
    [Display(Name = "Thất bại")] Failed,
    [Display(Name = "Đã hủy")] Cancelled,
    [Display(Name = "Đã hoàn tiền")] Refunded,
    [Display(Name = "Chờ chủ sân xác nhận")] AwaitingConfirmation
}

// Loại sân theo quy mô (chỉ áp dụng cho bóng đá và bóng rổ)
public enum CourtFormat
{
    [Display(Name = "Chưa phân loại")] None = 0,
    [Display(Name = "Sân 5 người")] Five = 1,
    [Display(Name = "Sân 7 người")] Seven = 2,
    [Display(Name = "Sân 11 người")] Eleven = 3,
    [Display(Name = "Nửa sân (3x3)")] HalfCourt = 4,
    [Display(Name = "Cả sân (5x5)")] FullCourt = 5
}

// Mặt sân
public enum CourtSurface
{
    [Display(Name = "Chưa xác định")] Unspecified = 0,
    [Display(Name = "Sân cứng")] Hard = 1,
    [Display(Name = "Đất nện")] Clay = 2,
    [Display(Name = "Cỏ nhân tạo")] ArtificialGrass = 3,
    [Display(Name = "Cỏ tự nhiên")] NaturalGrass = 4,
    [Display(Name = "Sàn gỗ")] Wood = 5,
    [Display(Name = "Thảm nhựa PVC")] PvcMat = 6
}

// Trạng thái một ô (sân x khung giờ) trên lịch dạng lưới
public enum SlotState
{
    Free,     // còn trống (xanh lá)
    Booked,   // đã đặt, lịch đã xác nhận (đỏ)
    Held,     // đang có người giữ chỗ, chờ thanh toán (vàng)
    Past,     // giờ đã qua
    Closed    // sân đang bảo trì
}
