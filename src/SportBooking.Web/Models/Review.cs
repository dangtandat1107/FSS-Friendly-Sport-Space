namespace SportBooking.Web.Models;

// Đánh giá của khách sau khi khung giờ đã đặt kết thúc. Mỗi lịch đặt chỉ được đánh giá một lần.
public class Review
{
    public int Id { get; set; }

    public string CustomerId { get; set; } = "";     // người đánh giá
    public ApplicationUser? Customer { get; set; }

    public int CourtId { get; set; }                 // sân được đánh giá
    public Court? Court { get; set; }

    public int BookingId { get; set; }               // lịch đặt làm căn cứ để đánh giá
    public Booking? Booking { get; set; }

    public int Rating { get; set; }                  // 1 đến 5 sao
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
