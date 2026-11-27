using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Extensions;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

// Nhân viên sân xem các đơn nước cần chuẩn bị theo từng khung giờ và cập nhật Đã chuẩn bị / Đã giao.
// Nhân viên chỉ thấy sân được giao, chủ sân thấy sân của mình, Admin thấy tất cả (xem IDrinkOrderService.VisibleCourtIdsAsync).
[Authorize(Roles = "Staff,Owner,Admin")]
public class DrinkOrdersController : Controller
{
    private readonly AppDbContext _db;
    private readonly IDrinkOrderService _drinks;
    public DrinkOrdersController(AppDbContext db, IDrinkOrderService drinks) { _db = db; _drinks = drinks; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string Role => User.IsInRole("Admin") ? "Admin" : User.IsInRole("Owner") ? "Owner" : "Staff";

    public async Task<IActionResult> Index(DateTime? date)
    {
        var d = date?.Date ?? DateTime.Today;
        var courtIds = await _drinks.VisibleCourtIdsAsync(UserId, Role);

        // Chỉ hiện đơn của lịch đã thanh toán (đã xác nhận); đơn chờ thanh toán hoặc đã hủy thì nhân viên chưa cần chuẩn bị
        var shown = new[] { DrinkOrderStatus.ToPrepare, DrinkOrderStatus.Prepared, DrinkOrderStatus.Delivered, DrinkOrderStatus.NoShow };
        var q = _db.Bookings.AsNoTracking().Include(b => b.Court).Include(b => b.Customer).Include(b => b.Items)
            .Where(b => b.Status == BookingStatus.Confirmed && shown.Contains(b.DrinkStatus) && b.StartTime >= d && b.StartTime < d.AddDays(1));
        if (courtIds != null) q = q.Where(b => courtIds.Contains(b.CourtId));

        return View(new DrinkOrdersVm
        {
            Date = d,
            Orders = await q.OrderBy(b => b.StartTime).ThenBy(b => b.Court!.Name).ToListAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, DrinkOrderStatus to, DateTime? date)
    {
        var (ok, error) = await _drinks.SetStatusAsync(UserId, Role, id, to);
        if (ok) TempData["Toast"] = $"✅ Đã chuyển sang \"{to.DisplayName()}\"";
        else TempData["Err"] = error;
        return RedirectToAction(nameof(Index), new { date = (date ?? DateTime.Today).ToString("yyyy-MM-dd") });
    }
}
