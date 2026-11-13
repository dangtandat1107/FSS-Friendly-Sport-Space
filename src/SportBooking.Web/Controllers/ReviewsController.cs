using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

[Authorize(Roles = "Customer")]
public class ReviewsController : Controller
{
    private readonly AppDbContext _db;
    public ReviewsController(AppDbContext db) { _db = db; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Create(int bookingId)
    {
        var (b, error) = await LoadEligibleAsync(bookingId);
        if (b == null) return Back(error!);
        return View(ToForm(b, new ReviewFormVm()));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReviewFormVm vm)
    {
        var (b, error) = await LoadEligibleAsync(vm.BookingId);
        if (b == null) return Back(error!);

        ToForm(b, vm);
        if (!ModelState.IsValid) return View(vm);

        _db.Reviews.Add(new Review
        {
            CustomerId = UserId, CourtId = b.CourtId, BookingId = b.Id,
            Rating = vm.Rating,
            Comment = string.IsNullOrWhiteSpace(vm.Comment) ? null : vm.Comment.Trim()
        });
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // Hai lần gửi cùng lúc: chỉ mục duy nhất trên BookingId chặn lần thứ hai
            return Back("Bạn đã đánh giá lịch này rồi.");
        }

        TempData["Msg"] = "Cảm ơn bạn đã đánh giá sân.";
        return RedirectToAction("Details", "Courts", new { id = b.CourtId });
    }

    // Chỉ đánh giá được lịch của chính mình, đã xác nhận, đã kết thúc giờ chơi và chưa đánh giá
    private async Task<(Booking? Booking, string? Error)> LoadEligibleAsync(int bookingId)
    {
        var b = await _db.Bookings.Include(x => x.Court).Include(x => x.Review)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.CustomerId == UserId);
        if (b == null) return (null, "Không tìm thấy lịch đặt.");
        if (b.Status != BookingStatus.Confirmed) return (null, "Chỉ đánh giá được lịch đã xác nhận.");
        if (b.EndTime > DateTime.Now) return (null, "Bạn có thể đánh giá sau khi khung giờ đặt sân kết thúc.");
        if (b.Review != null) return (null, "Bạn đã đánh giá lịch này rồi.");
        return (b, null);
    }

    private static ReviewFormVm ToForm(Booking b, ReviewFormVm vm)
    {
        vm.BookingId = b.Id;
        vm.CourtName = b.Court!.Name;
        vm.PlayedAt = $"{b.StartTime:dd/MM/yyyy HH:mm} - {b.EndTime:HH:mm}";
        return vm;
    }

    private IActionResult Back(string error)
    {
        TempData["Err"] = error;
        return RedirectToAction("My", "Bookings");
    }
}
