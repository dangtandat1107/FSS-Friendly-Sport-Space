using SportBooking.Web.Models;
namespace SportBooking.Web.ViewModels;

public class CourtListVm
{
    public List<Court> Courts { get; set; } = new();
    public SportType? Sport { get; set; }
    public CourtFormat? Format { get; set; }
    public CourtSurface? Surface { get; set; }
    public bool? Indoor { get; set; }
    public string? District { get; set; }
    public Dictionary<CourtFormat, int> FormatCounts { get; set; } = new();   // số sân đang mở theo từng loại, trong môn đang chọn
}
