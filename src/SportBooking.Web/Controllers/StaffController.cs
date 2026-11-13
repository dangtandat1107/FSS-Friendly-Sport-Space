using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

// Chủ sân tạo và quản lý nhân viên cho các sân của mình. Nhân viên chỉ xử lý sự cố, không có quyền duyệt lịch, thanh toán hay quản lý sân.
[Authorize(Roles = "Owner,Admin")]
public class StaffController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _um;

    public StaffController(AppDbContext db, UserManager<ApplicationUser> um) { _db = db; _um = um; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Sân mà người này được quyền giao việc: sân của mình (Admin thì tất cả)
    private IQueryable<Court> MyCourts()
    {
        var q = _db.Courts.AsQueryable();
        return User.IsInRole("Admin") ? q : q.Where(c => c.OwnerId == UserId);
    }

    public async Task<IActionResult> Index()
    {
        var courtIds = await MyCourts().Select(c => c.Id).ToListAsync();
        var rows = await _db.Staffs.AsNoTracking().Include(s => s.User).Include(s => s.Court)
            .Where(s => courtIds.Contains(s.CourtId))
            .OrderBy(s => s.User!.FullName).ThenBy(s => s.Court!.Name).ToListAsync();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadCourtsAsync();
        return View(new StaffFormVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StaffFormVm vm)
    {
        // Chỉ được giao các sân thuộc quyền của mình, bỏ mọi mã sân khác do người dùng tự gửi lên
        var allowed = await MyCourts().Select(c => c.Id).ToListAsync();
        var courtIds = vm.CourtIds.Distinct().Where(allowed.Contains).ToList();
        if (courtIds.Count == 0) ModelState.AddModelError("", "Vui lòng chọn ít nhất một sân cho nhân viên.");
        if (!ModelState.IsValid) { await LoadCourtsAsync(); return View(vm); }

        var user = new ApplicationUser { UserName = vm.Email, Email = vm.Email, FullName = vm.FullName.Trim(), EmailConfirmed = true };
        var created = await _um.CreateAsync(user, vm.Password);
        if (!created.Succeeded)
        {
            foreach (var e in created.Errors) ModelState.AddModelError("", e.Description);
            await LoadCourtsAsync();
            return View(vm);
        }
        await _um.AddToRoleAsync(user, "Staff");

        foreach (var cid in courtIds) _db.Staffs.Add(new Staff { UserId = user.Id, CourtId = cid });
        await _db.SaveChangesAsync();

        TempData["Msg"] = $"Đã tạo tài khoản nhân viên {user.FullName}. Hãy gửi email đăng nhập và mật khẩu tạm cho họ.";
        return RedirectToAction(nameof(Index));
    }

    // Tạm dừng hoặc cho làm lại tại một sân. Tạm dừng thì nhân viên không còn thấy sự cố của sân đó
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var courtIds = await MyCourts().Select(c => c.Id).ToListAsync();
        var s = await _db.Staffs.FirstOrDefaultAsync(x => x.Id == id && courtIds.Contains(x.CourtId));
        if (s == null) return NotFound();
        s.IsActive = !s.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadCourtsAsync()
        => ViewBag.Courts = await MyCourts().AsNoTracking().OrderBy(c => c.Name).ToListAsync();
}
