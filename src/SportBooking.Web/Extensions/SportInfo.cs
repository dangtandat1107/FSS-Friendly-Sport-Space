using SportBooking.Web.Models;
namespace SportBooking.Web.Extensions;

// Thông tin hiển thị của từng môn: tên, màu mặt sân, ảnh minh họa.
// Muốn đổi ảnh hoặc màu, chỉ cần sửa ở đây.
public record SportMeta(string Key, string Label, string Color, string Image, string Tagline);

public static class SportInfo
{
    public static IEnumerable<SportType> All => Enum.GetValues<SportType>();

    public static SportMeta Meta(this SportType sport) => sport switch
    {
        SportType.Badminton  => new("badminton",  "Cầu lông",   "#0e8f8a", "/images/sports/badminton.svg",  "Sân trong nhà, đánh theo giờ"),
        SportType.Football   => new("football",   "Bóng đá",    "#4f9d2d", "/images/sports/football.svg",   "Sân cỏ nhân tạo, đá theo giờ"),
        SportType.Tennis     => new("tennis",     "Tennis",     "#2d68b8", "/images/sports/tennis.svg",     "Sân cứng, đặt theo giờ"),
        SportType.Pickleball => new("pickleball", "Pickleball", "#6d4fc2", "/images/sports/pickleball.svg", "Dễ làm quen, hợp chơi theo nhóm"),
        SportType.Basketball => new("basketball", "Bóng rổ",    "#b9631f", "/images/sports/basketball.svg", "Sân rộng cho cả nhóm"),
        _                    => new("badminton",  sport.ToString(), "#12804f", "/images/sports/badminton.svg", "")
    };
}
