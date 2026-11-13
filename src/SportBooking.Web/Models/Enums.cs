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

// ---------- Ghi nhận sự cố ----------
// Không đổi thứ tự hoặc xóa giá trị đã có, vì database lưu số nguyên của enum

public enum IncidentCategory
{
    [Display(Name = "Y tế, chấn thương")] Medical,
    [Display(Name = "Cơ sở vật chất")] Facility,
    [Display(Name = "Vận hành")] Operational,
    [Display(Name = "An ninh, mất đồ")] Security
}

public enum IncidentSeverity
{
    [Display(Name = "Thấp")] Low,
    [Display(Name = "Trung bình")] Medium,
    [Display(Name = "Cao")] High,
    [Display(Name = "Khẩn cấp (SOS)")] Critical
}

public enum IncidentStatus
{
    [Display(Name = "Mới báo")] Reported,
    [Display(Name = "Đã tiếp nhận")] Acknowledged,
    [Display(Name = "Đang xử lý")] InProgress,
    [Display(Name = "Đã xử lý xong")] Resolved,
    [Display(Name = "Đã leo thang lên Admin")] Escalated,
    [Display(Name = "Đã đóng")] Closed,
    [Display(Name = "Bị từ chối")] Rejected
}

// Ai tạo ra sự cố. Hiện chỉ khách hàng báo; các giá trị còn lại để mở rộng (nhân viên hoặc chủ sân báo hộ)
public enum IncidentSource
{
    [Display(Name = "Khách hàng")] Customer,
    [Display(Name = "Nhân viên")] Staff,
    [Display(Name = "Chủ sân")] Owner,
    [Display(Name = "Hệ thống")] SystemAuto
}

// Lý do khiếu nại ở luồng phản hồi sau giờ chơi (khách chọn từ danh sách)
public enum ComplaintReason
{
    [Display(Name = "Đau nhức, chấn thương sau buổi chơi")] LateSymptoms,
    [Display(Name = "Sân không đúng mô tả")] NotAsDescribed,
    [Display(Name = "Để quên hoặc mất đồ")] LostItem,
    [Display(Name = "Khiếu nại thanh toán")] PaymentDispute,
    [Display(Name = "Lý do khác")] Other
}

// Chủ sân đề xuất bồi thường cho luồng phản hồi. Hệ thống chỉ ghi nhận, không tự chuyển tiền
public enum CompensationType
{
    [Display(Name = "Chưa đề xuất")] None,
    [Display(Name = "Lời xin lỗi")] Apology,
    [Display(Name = "Voucher giảm giá")] Voucher,
    [Display(Name = "Hoàn tiền")] Refund
}
