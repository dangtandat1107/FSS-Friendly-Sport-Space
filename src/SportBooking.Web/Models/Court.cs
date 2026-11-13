using System.ComponentModel.DataAnnotations.Schema;
namespace SportBooking.Web.Models;

public class Court
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public SportType SportType { get; set; }
    public string District { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal PricePerHour { get; set; }
    public CourtStatus Status { get; set; } = CourtStatus.Active;

    public CourtFormat CourtFormat { get; set; } = CourtFormat.None;       // loại sân theo quy mô
    public CourtSurface Surface { get; set; } = CourtSurface.Unspecified;  // mặt sân
    public bool IsIndoor { get; set; }                                      // trong nhà (có mái che)

    public string OwnerId { get; set; } = "";
    public ApplicationUser? Owner { get; set; }
    public List<Booking> Bookings { get; set; } = new();

    // Điểm trung bình và số lượt đánh giá. Không lưu trong bảng Courts, được tính từ bảng Reviews
    // bởi ReviewExtensions.LoadRatingsAsync trước khi hiển thị.
    [NotMapped] public double? AvgRating { get; set; }
    [NotMapped] public int ReviewCount { get; set; }
}
