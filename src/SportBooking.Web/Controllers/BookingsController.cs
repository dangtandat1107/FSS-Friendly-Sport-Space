using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
namespace SportBooking.Web.Controllers;

[Authorize]
public class BookingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IBookingService _svc;
    public BookingsController(AppDbContext db, IBookingService svc) { _db = db; _svc = svc; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int courtId, DateTime date, int hour, int hours = 1)
    {
        var r = await _svc.CreateAsync(UserId, courtId, date.Date.AddHours(hour), hours);
        if (r.Success)
        {
            TempData["Msg"] = $"Đã giữ chỗ. Vui lòng thanh toán trong {IBookingService.PaymentTimeoutMinutes} phút.";
            return RedirectToAction("Pay", "Payments", new { bookingId = r.Booking!.Id });
        }
        TempData["Err"] = r.Error;
        return RedirectToAction("Details", "Courts", new { id = courtId, date = date.ToString("yyyy-MM-dd") });
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> My() =>
        View(await _db.Bookings.AsNoTracking().Include(b => b.Court).Include(b => b.Payments)
            .Where(b => b.CustomerId == UserId).OrderByDescending(b => b.StartTime).ToListAsync());

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var b = await _db.Bookings.Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == UserId);
        if (b == null) return NotFound();
        if (b.Status == BookingStatus.Cancelled) return RedirectToAction(nameof(My));

        // Lịch đã xác nhận (đã trả tiền) chỉ được hủy trước giờ chơi ít nhất 2 giờ.
        // Lịch chưa thanh toán thì hủy tự do.
        if (b.Status == BookingStatus.Confirmed && b.StartTime < DateTime.Now.AddHours(2))
        {
            TempData["Err"] = "Chỉ được hủy trước giờ chơi ít nhất 2 giờ.";
            return RedirectToAction(nameof(My));
        }

        var hadPaid = b.Payments.Any(p => p.Status == PaymentStatus.Paid);
        ApplyCancel(b);
        await _db.SaveChangesAsync();
        TempData["Msg"] = hadPaid ? "Đã hủy lịch. Khoản thanh toán được ghi nhận hoàn tiền (giả lập)." : "Đã hủy lịch.";
        return RedirectToAction(nameof(My));
    }

    // ---------- Chủ sân xem và xử lý lịch ----------
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Manage()
    {
        var q = _db.Bookings.AsNoTracking().Include(b => b.Court).Include(b => b.Customer).Include(b => b.Payments).AsQueryable();
        if (!User.IsInRole("Admin")) q = q.Where(b => b.Court!.OwnerId == UserId);
        return View(await q.OrderByDescending(b => b.StartTime).ToListAsync());
    }

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(int id, BookingStatus status)
    {
        var b = await _db.Bookings.Include(x => x.Court).Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id && (User.IsInRole("Admin") || x.Court!.OwnerId == UserId));
        if (b == null) return NotFound();

        if (status == BookingStatus.Cancelled) ApplyCancel(b);
        else if (status == BookingStatus.Confirmed) b.Status = BookingStatus.Confirmed;   // xác nhận thủ công (ví dụ khách trả tiền mặt tại sân)
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Manage));
    }

    // Hủy lịch: giao dịch đang chờ thì hủy, giao dịch đã trả thì ghi nhận hoàn tiền (giả lập)
    private static void ApplyCancel(Booking b)
    {
        b.Status = BookingStatus.Cancelled;
        foreach (var p in b.Payments)
        {
            if (p.Status == PaymentStatus.Pending) p.Status = PaymentStatus.Cancelled;
            else if (p.Status == PaymentStatus.Paid) p.Status = PaymentStatus.Refunded;
        }
    }
}
