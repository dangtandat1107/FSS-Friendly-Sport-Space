using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
namespace SportBooking.Web.Controllers;

[Authorize(Roles = "Customer")]
public class PaymentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPaymentService _svc;
    public PaymentsController(AppDbContext db, IPaymentService svc) { _db = db; _svc = svc; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Bước 1: xem lại lịch đặt và chọn phương thức thanh toán
    public async Task<IActionResult> Pay(int bookingId)
    {
        var b = await _db.Bookings.AsNoTracking().Include(x => x.Court)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.CustomerId == UserId);
        if (b == null) return NotFound();
        if (b.Status != BookingStatus.Pending || b.IsExpired)
        {
            TempData["Err"] = "Lịch này không còn chờ thanh toán.";
            return RedirectToAction("My", "Bookings");
        }
        return View(b);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int bookingId, PaymentMethod method)
    {
        var r = await _svc.StartAsync(UserId, bookingId, method);
        if (!r.Success) { TempData["Err"] = r.Error; return RedirectToAction("My", "Bookings"); }
        return RedirectToAction(nameof(Gateway), new { id = r.Payment!.Id });
    }

    // Bước 2: màn hình cổng thanh toán giả lập
    public async Task<IActionResult> Gateway(int id)
    {
        var p = await _db.Payments.AsNoTracking()
            .Include(x => x.Booking).ThenInclude(b => b!.Court)
            .FirstOrDefaultAsync(x => x.Id == id && x.Booking!.CustomerId == UserId);
        if (p == null) return NotFound();
        if (p.Status != PaymentStatus.Pending)
        {
            TempData["Err"] = "Giao dịch này đã được xử lý.";
            return RedirectToAction("My", "Bookings");
        }
        return View(p);
    }

    // Bước 3: mô phỏng kết quả thanh toán
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int paymentId, bool success)
    {
        var r = await _svc.CompleteAsync(UserId, paymentId, success);
        if (r.Success) TempData["Msg"] = "Thanh toán thành công. Lịch đặt của bạn đã được xác nhận.";
        else TempData["Err"] = r.Error;
        return RedirectToAction("My", "Bookings");
    }
}
