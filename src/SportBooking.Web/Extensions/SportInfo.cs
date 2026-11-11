using Microsoft.Extensions.FileProviders;
using SportBooking.Web.Models;
namespace SportBooking.Web.Extensions;

// Thông tin hiển thị của từng môn: tên, màu, ảnh. Muốn đổi màu hay lời giới thiệu, sửa ở đây.
//
// Ảnh: nếu trong wwwroot/images/sports có ảnh thật đặt tên photo-<môn>.jpg (hoặc .jpeg, .png, .webp)
// thì dùng ảnh đó, không có thì dùng ảnh minh họa <môn>.svg. Ví dụ: photo-badminton.jpg, photo-football.jpg,
// photo-tennis.jpg, photo-pickleball.jpg, photo-basketball.jpg
public record SportMeta(string Key, string Label, string Color, string Image, string Tagline);

public static class SportInfo
{
    private static IFileProvider? _webRoot;
    public static void UseWebRoot(IFileProvider webRoot) => _webRoot = webRoot;

    public static IEnumerable<SportType> All => Enum.GetValues<SportType>();

    public static SportMeta Meta(this SportType sport) => sport switch
    {
        SportType.Badminton  => Build("badminton",  "Cầu lông",   "#0e8f8a", "Sân trong nhà, đánh theo giờ"),
        SportType.Football   => Build("football",   "Bóng đá",    "#4f9d2d", "Sân cỏ nhân tạo, đá theo giờ"),
        SportType.Tennis     => Build("tennis",     "Tennis",     "#2d68b8", "Sân cứng, đặt theo giờ"),
        SportType.Pickleball => Build("pickleball", "Pickleball", "#6d4fc2", "Dễ làm quen, hợp chơi theo nhóm"),
        SportType.Basketball => Build("basketball", "Bóng rổ",    "#b9631f", "Sân rộng cho cả nhóm"),
        _                    => Build("badminton",  sport.ToString(), "#12804f", "")
    };

    private static SportMeta Build(string key, string label, string color, string tagline) =>
        new(key, label, color, PickImage(key), tagline);

    private static string PickImage(string key)
    {
        foreach (var ext in new[] { "jpg", "jpeg", "png", "webp" })
        {
            var path = $"/images/sports/photo-{key}.{ext}";
            if (_webRoot?.GetFileInfo(path).Exists == true) return path;
        }
        return $"/images/sports/{key}.svg";
    }
}
