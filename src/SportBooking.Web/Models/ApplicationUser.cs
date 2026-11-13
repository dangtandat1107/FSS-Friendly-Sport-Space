using Microsoft.AspNetCore.Identity;
namespace SportBooking.Web.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";

    // Điểm uy tín từ 0 đến 100 (khách mới là 100). Báo sự cố bị từ chối thì giảm, báo đúng thì tăng. Xem IncidentService
    public int TrustScore { get; set; } = 100;
}
