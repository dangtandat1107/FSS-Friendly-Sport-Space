using SportBooking.Web.Models;
namespace SportBooking.Web.ViewModels;

public class HomeVm
{
    public List<Court> Featured { get; set; } = new();
    public Dictionary<SportType, int> CountBySport { get; set; } = new();
}
