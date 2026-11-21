namespace SportBooking.Web.Models;

// Thống kê tập luyện của một khách hàng. Chuỗi cho phép nghỉ: nghỉ tối đa 2 ngày liên tiếp vẫn giữ chuỗi (xem StreakPolicy). Đây là bản lưu sẵn để đọc nhanh; số liệu luôn được tính lại từ bảng Bookings
// (xem StreakService) nên không bao giờ lệch so với dữ liệu thật. Chỉ tính các buổi đã xác nhận và đã tới ngày chơi.
public class UserStats
{
    public string UserId { get; set; } = "";             // khóa chính, quan hệ 1-1 với ApplicationUser
    public ApplicationUser? User { get; set; }

    public int CurrentStreak { get; set; }                // số ngày liên tiếp hiện tại (0 nếu đã đứt chuỗi)
    public int LongestStreak { get; set; }                // chuỗi dài nhất từ trước đến nay
    public int TotalBookings { get; set; }                // tổng số lần đặt sân
    public int MonthlyBookings { get; set; }              // số ngày có đặt sân trong tháng hiện tại
    public int YearlyBookings { get; set; }               // số ngày có đặt sân trong năm hiện tại
    public int GraceDaysUsedThisMonth { get; set; }       // số ngày nghỉ hợp lý (không làm mất chuỗi) đã dùng trong tháng hiện tại
    public DateTime? LastBookingDate { get; set; }        // ngày chơi gần nhất (chỉ lấy phần ngày)
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

// Danh mục huy hiệu. Lưu trong database để thêm hoặc đổi mốc mà không phải sửa code
public class Badge
{
    public int Id { get; set; }
    public string Code { get; set; } = "";               // mã cố định, ví dụ "warrior"
    public string Name { get; set; } = "";
    public string Icon { get; set; } = "";
    public int RequiredStreakDays { get; set; }           // đạt khi chuỗi dài nhất từ mốc này trở lên
    public string Description { get; set; } = "";
}

// Huy hiệu mà một khách đã đạt. Đã đạt thì giữ mãi, kể cả khi chuỗi sau đó bị đứt
public class UserBadge
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public int BadgeId { get; set; }
    public Badge? Badge { get; set; }
    public DateTime EarnedAt { get; set; } = DateTime.Now;
    public bool IsSeen { get; set; }                      // khách đã được báo huy hiệu này (toast) chưa
}
