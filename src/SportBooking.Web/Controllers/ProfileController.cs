using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Extensions;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _um;
    private readonly SignInManager<ApplicationUser> _sm;
    private readonly AppDbContext _db;
    private readonly IAvatarService _avatars;

    public ProfileController(UserManager<ApplicationUser> um, SignInManager<ApplicationUser> sm, AppDbContext db, IAvatarService avatars)
    { _um = um; _sm = sm; _db = db; _avatars = avatars; }

    public async Task<IActionResult> Index(string? tab)
    {
        var user = await _um.GetUserAsync(User);
        if (user == null) return Challenge();
        var active = tab is "password" or "upcoming" or "courts" ? tab : "info";
        return View(await BuildAsync(user, active));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Edit")] UpdateProfileVm vm)
    {
        var user = await _um.GetUserAsync(User);
        if (user == null) return Challenge();

        if (!ModelState.IsValid)
        {
            var page = await BuildAsync(user, "info");
            page.Edit = vm;
            return View("Index", page);
        }

        user.FullName = vm.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(vm.PhoneNumber) ? null : vm.PhoneNumber.Trim();
        var r = await _um.UpdateAsync(user);
        if (!r.Succeeded)
        {
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
            var page = await BuildAsync(user, "info");
            page.Edit = vm;
            return View("Index", page);
        }

        await _sm.RefreshSignInAsync(user);   // cập nhật tên trên thanh menu
        TempData["Msg"] = "Đã cập nhật hồ sơ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "ChangePassword")] ChangePasswordVm vm)
    {
        var user = await _um.GetUserAsync(User);
        if (user == null) return Challenge();

        if (ModelState.IsValid)
        {
            var r = await _um.ChangePasswordAsync(user, vm.CurrentPassword, vm.NewPassword);
            if (r.Succeeded)
            {
                await _sm.RefreshSignInAsync(user);   // giữ đăng nhập sau khi đổi mật khẩu
                TempData["Msg"] = "Đổi mật khẩu thành công.";
                return RedirectToAction(nameof(Index));
            }
            foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
        }
        return View("Index", await BuildAsync(user, "password"));
    }

    // ---------- Ảnh đại diện ----------
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile? avatar)
    {
        var user = await _um.GetUserAsync(User);
        if (user == null) return Challenge();
        if (avatar == null || avatar.Length == 0)
        {
            TempData["Err"] = "Bạn chưa chọn ảnh.";
            return RedirectToAction(nameof(Index));
        }

        var (ok, error) = await _avatars.SaveAsync(user.Id, avatar);
        if (!ok) { TempData["Err"] = error; return RedirectToAction(nameof(Index)); }

        await _sm.RefreshSignInAsync(user);   // cập nhật ảnh trên thanh menu
        TempData["Msg"] = "Đã cập nhật ảnh đại diện.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar()
    {
        var user = await _um.GetUserAsync(User);
        if (user == null) return Challenge();
        _avatars.Delete(user.Id);
        await _sm.RefreshSignInAsync(user);
        TempData["Msg"] = "Đã xóa ảnh đại diện.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ProfilePageVm> BuildAsync(ApplicationUser user, string tab)
    {
        var role = (await _um.GetRolesAsync(user)).FirstOrDefault() ?? "Customer";
        var vm = new ProfilePageVm
        {
            Email = user.Email ?? "", Role = role, FullName = user.FullName, ActiveTab = tab,
            AvatarUrl = _avatars.GetUrl(user.Id),
            Edit = new UpdateProfileVm { FullName = user.FullName, PhoneNumber = user.PhoneNumber }
        };
        var now = DateTime.Now;

        if (role == "Customer")
        {
            // Lịch còn hiệu lực: đã xác nhận, hoặc đang chờ thanh toán mà chưa quá hạn
            var mine = _db.Bookings.AsNoTracking().Where(b => b.CustomerId == user.Id &&
                (b.Status == BookingStatus.Confirmed ||
                 (b.Status == BookingStatus.Pending && (b.ExpiresAt == null || b.ExpiresAt > now))));
            var spent = await mine.Where(b => b.Status == BookingStatus.Confirmed).SumAsync(b => (decimal?)b.TotalPrice) ?? 0;
            var total = await mine.CountAsync();

            vm.Stats.Add(new("Tổng lượt đặt", total.ToString()));
            vm.Stats.Add(new("Sắp diễn ra", (await mine.CountAsync(b => b.EndTime > now)).ToString()));
            vm.Stats.Add(new("Đã chi (đã xác nhận)", spent.ToString("N0") + " đ"));

            if (total > 0)
            {
                var fav = await mine.GroupBy(b => b.Court!.SportType)
                    .Select(g => new { Sport = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count).FirstOrDefaultAsync();
                if (fav != null) vm.Stats.Add(new("Môn chơi nhiều nhất", fav.Sport.DisplayName()));
            }

            vm.Upcoming = await _db.Bookings.AsNoTracking().Include(b => b.Court)
                .Where(b => b.CustomerId == user.Id && b.StartTime >= now &&
                    (b.Status == BookingStatus.Confirmed ||
                     (b.Status == BookingStatus.Pending && (b.ExpiresAt == null || b.ExpiresAt > now))))
                .OrderBy(b => b.StartTime).Take(5).ToListAsync();
        }
        else if (role == "Owner")
        {
            var courts = await _db.Courts.AsNoTracking().Where(c => c.OwnerId == user.Id).OrderBy(c => c.Name).ToListAsync();
            var awaiting = await _db.Payments.CountAsync(p => p.Status == PaymentStatus.AwaitingConfirmation && p.Booking!.Court!.OwnerId == user.Id);
            var revenue = await _db.Bookings
                .Where(b => b.Court!.OwnerId == user.Id && b.Status == BookingStatus.Confirmed)
                .SumAsync(b => (decimal?)b.TotalPrice) ?? 0;

            vm.MyCourts = courts;
            vm.Stats.Add(new("Số sân của tôi", courts.Count.ToString()));
            vm.Stats.Add(new("Chờ xác nhận chuyển khoản", awaiting.ToString()));
            vm.Stats.Add(new("Doanh thu đã xác nhận", revenue.ToString("N0") + " đ"));
        }
        return vm;
    }
}
