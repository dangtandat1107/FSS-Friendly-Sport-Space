using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

// Thêm "FullName" và "Avatar" vào cookie để thanh menu hiện tên, ảnh mà không cần truy vấn DB mỗi trang
public class AppClaimsFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    private readonly IAvatarService _avatars;

    public AppClaimsFactory(UserManager<ApplicationUser> um, RoleManager<IdentityRole> rm,
        IOptions<IdentityOptions> opt, IAvatarService avatars) : base(um, rm, opt)
    { _avatars = avatars; }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var id = await base.GenerateClaimsAsync(user);
        id.AddClaim(new Claim("FullName", user.FullName ?? ""));
        var avatar = _avatars.GetUrl(user.Id);
        if (avatar != null) id.AddClaim(new Claim("Avatar", avatar));
        return id;
    }
}
