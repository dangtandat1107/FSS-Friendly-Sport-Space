using SportBooking.Web.Models;
namespace SportBooking.Web.Extensions;

// Mỗi môn có những loại sân và mặt sân riêng. Muốn thêm hoặc bớt lựa chọn, chỉ sửa ở đây.
public static class CourtOptions
{
    // Loại sân theo quy mô: chỉ bóng đá và bóng rổ chia theo số người.
    // Pickleball đánh đơn hay đôi đều dùng cùng một loại sân (13,41 x 6,10 m) nên không chia loại.
    public static CourtFormat[] FormatsFor(SportType sport) => sport switch
    {
        SportType.Football   => new[] { CourtFormat.Five, CourtFormat.Seven, CourtFormat.Eleven },
        SportType.Basketball => new[] { CourtFormat.HalfCourt, CourtFormat.FullCourt },
        _                    => Array.Empty<CourtFormat>()
    };

    public static CourtSurface[] SurfacesFor(SportType sport) => sport switch
    {
        SportType.Football   => new[] { CourtSurface.ArtificialGrass, CourtSurface.NaturalGrass },
        SportType.Tennis     => new[] { CourtSurface.Hard, CourtSurface.Clay },
        SportType.Badminton  => new[] { CourtSurface.PvcMat, CourtSurface.Wood },
        SportType.Basketball => new[] { CourtSurface.Wood, CourtSurface.Hard },
        SportType.Pickleball => new[] { CourtSurface.Hard, CourtSurface.Wood },
        _                    => Array.Empty<CourtSurface>()
    };

    // Bóng đá luôn là sân ngoài trời nên không có lựa chọn trong nhà/ngoài trời (cả bộ lọc lẫn form thêm sân)
    public static bool HasIndoorOption(SportType sport) => sport != SportType.Football;

    // Các nhãn ngắn hiển thị trên thẻ sân
    public static List<string> Tags(Court c)
    {
        var tags = new List<string>();
        if (c.CourtFormat != CourtFormat.None) tags.Add(c.CourtFormat.DisplayName());
        if (c.Surface != CourtSurface.Unspecified) tags.Add(c.Surface.DisplayName());
        tags.Add(c.IsIndoor ? "Trong nhà" : "Ngoài trời");
        return tags;
    }
}
