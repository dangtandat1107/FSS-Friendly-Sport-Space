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

[Authorize]
public class BookingsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IBookingService _svc;
    private readonly IStreakService _streaks;
    private readonly IProductService _products;
    private readonly IPromotionService _promos;
    private readonly IDrinkOrderService _drinks;
    private readonly IIncidentService _incidents;
    private readonly IImageStore _images;

    public BookingsController(AppDbContext db, IBookingService svc, IStreakService streaks, IProductService products,
        IPromotionService promos, IDrinkOrderService drinks, IIncidentService incidents, IImageStore images)
    { _db = db; _svc = svc; _streaks = streaks; _products = products; _promos = promos; _drinks = drinks; _incidents = incidents; _images = images; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int courtId, DateTime date, int hour, int hours = 1)
    {
        var r = await _svc.CreateAsync(UserId, courtId, date.Date.AddHours(hour), hours);
        if (r.Success)
        {
            TempData["Toast"] = $"✅ Đặt sân thành công! Mã đơn: {r.Booking!.Code}";
            TempData["Msg"] = $"Đã giữ chỗ. Vui lòng thanh toán trong {IBookingService.PaymentTimeoutMinutes} phút.";
            return RedirectToAction("Pay", "Payments", new { bookingId = r.Booking!.Id });
        }
        TempData["Err"] = r.Error;
        return RedirectToAction("Details", "Courts", new { id = courtId, date = date.ToString("yyyy-MM-dd") });
    }

    // ---------- Đặt sân kèm đồ uống (hộp thoại trên lịch dạng lưới) ----------

    // Danh sách sản phẩm đang bán của sân, kèm số còn có thể đặt, chuỗi tập luyện của khách và các khuyến mãi đang bật. Hộp thoại gọi bằng AJAX
    [Authorize(Roles = "Customer"), HttpGet]
    public async Task<IActionResult> GetProductsByCourt(int courtId)
    {
        if (!await _db.Courts.AnyAsync(c => c.Id == courtId)) return NotFound();
        Response.Headers.CacheControl = "no-store";

        var stock = await _products.GetAvailableAsync(courtId);
        var streak = (await _streaks.GetStatsAsync(UserId)).CurrentStreak;   // lấy qua IStreakService để chuỗi luôn được làm mới
        return Json(new OrderDrinksViewModel
        {
            CourtId = courtId,
            StreakDays = streak,
            MaxPerProduct = DrinkPolicy.MaxPerProduct,
            Products = stock.Select(s => new ProductCardVm
            {
                Id = s.Product.Id, Name = s.Product.Name, Description = s.Product.Description, Price = s.Product.Price,
                Category = s.Product.Category.DisplayName(), Icon = DrinkPolicy.Icon(s.Product.Category),
                ImageUrl = _images.GetUrl("products", s.Product.Id.ToString()),
                Available = s.Available, MaxQty = Math.Min(DrinkPolicy.MaxPerProduct, s.Available)
            }).ToList()
        });
    }

    // Xem trước đơn nước: server tính tiền nước, quà tặng và khuyến mãi. items có dạng "mãSP:số lượng,mãSP:số lượng"
    // Chỉ đọc, không ghi gì nên dùng GET.
    [Authorize(Roles = "Customer"), HttpGet]
    public async Task<IActionResult> QuoteDrinks(int courtId, string? items)
    {
        Response.Headers.CacheControl = "no-store";
        var streak = (await _streaks.GetStatsAsync(UserId)).CurrentStreak;
        var q = await _promos.QuoteAsync(courtId, ParseItems(items), streak);
        return Json(new OrderDrinksViewModel
        {
            CourtId = courtId, StreakDays = streak, Ok = q.Ok, Errors = q.Errors, DrinksTotal = q.DrinksTotal,
            Lines = q.Lines.Select(l => new OrderLineVm
            {
                ProductId = l.ProductId, Name = l.Name, Quantity = l.Quantity, UnitPrice = l.UnitPrice, LineTotal = l.LineTotal, IsFree = l.IsFree
            }).ToList(),
            Banners = q.Banners.Select(b => new PromoBannerVm { Kind = b.Kind, Text = b.Text }).ToList()
        });
    }

    private static List<OrderLine> ParseItems(string? items)
    {
        var list = new List<OrderLine>();
        foreach (var part in (items ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Take(30))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && int.TryParse(kv[0], out var id) && int.TryParse(kv[1], out var qty)) list.Add(new OrderLine(id, qty));
        }
        return list;
    }

    // Nút "Thêm vào đơn và thanh toán": tạo lịch kèm đơn nước. Giá, tồn kho và khuyến mãi được server tính lại, không tin dữ liệu từ trình duyệt
    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBookingWithItems(BookingViewModel model)
    {
        if (model.Items.Count > 50) return BadRequest();
        var streak = (await _streaks.GetStatsAsync(UserId)).CurrentStreak;
        var lines = model.Items.Select(i => new OrderLine(i.ProductId, i.Quantity)).ToList();

        var r = await _svc.CreateAsync(UserId, model.CourtId, model.Date.Date.AddHours(model.Hour), model.Hours, lines, streak);
        if (r.Success)
        {
            var b = r.Booking!;
            TempData["Toast"] = $"✅ Đặt sân thành công! Mã đơn: {b.Code}";
            TempData["Msg"] = (b.DrinkStatus == DrinkOrderStatus.None ? "" : "Đơn nước đã được thêm vào đơn. ")
                + $"Đã giữ chỗ. Vui lòng thanh toán trong {IBookingService.PaymentTimeoutMinutes} phút.";
            return RedirectToAction("Pay", "Payments", new { bookingId = b.Id });
        }
        TempData["Err"] = r.Error;
        return RedirectToAction("Details", "Courts", new { id = model.CourtId, date = model.Date.ToString("yyyy-MM-dd") });
    }

    // Chi tiết một lịch đặt: thông tin sân, thanh toán và "Đơn hàng kèm theo" (để khách xem và nhân viên biết cần chuẩn bị gì)
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Detail(int id)
    {
        var b = await _db.Bookings.AsNoTracking().Include(x => x.Court).Include(x => x.Payments).Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == UserId);
        if (b == null) return NotFound();
        return View("BookingDetail", b);
    }

    // Khách báo "không nhận được nước đã đặt": tạo sự cố tự động. Luồng SOS hay phản hồi do IncidentService tự xác định theo giờ
    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportMissingDrinks(int id)
    {
        var b = await _db.Bookings.AsNoTracking().Include(x => x.Court).Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == UserId);
        if (b == null) return NotFound();
        if (b.Status != BookingStatus.Confirmed || b.Items.Count == 0 || b.StartTime > DateTime.Now)
        {
            TempData["Err"] = "Chỉ báo được khi lịch đã xác nhận, có đơn nước và đã đến giờ chơi.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var list = string.Join(", ", b.Items.Select(i => $"{i.Quantity} x {i.ProductName}"));
        var r = await _incidents.CreateIncidentAsync(UserId, new CreateIncidentRequest(
            b.Id, IncidentCategory.Operational, ComplaintReason.Other,
            $"Không nhận được đồ uống đã đặt kèm lịch {b.Code} tại {b.Court?.Name}. Đơn nước: {list}."));
        if (!r.Ok) { TempData["Err"] = r.Error; return RedirectToAction(nameof(Detail), new { id }); }

        TempData["Toast"] = $"✅ Đã gửi báo cáo {r.Incident!.Code}. Chủ sân sẽ kiểm tra đơn nước giúp bạn.";
        return RedirectToAction("Details", "Incidents", new { id = r.Incident.Id });
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> My()
    {
        var list = await _db.Bookings.AsNoTracking().Include(b => b.Court).Include(b => b.Payments).Include(b => b.Review)
            .Where(b => b.CustomerId == UserId).OrderByDescending(b => b.StartTime).ToListAsync();

        // Chuỗi tập luyện hiện ở đầu trang; huy hiệu mới đạt thì báo bằng toast
        var streak = await _streaks.GetSummaryAsync(UserId);
        ViewData["Streak"] = streak;
        var toast = BadgeToast.For(streak.NewlyEarned);
        if (toast != null) TempData["Toast"] = toast;
        return View(list);
    }

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
        var hadAwaiting = b.Payments.Any(p => p.Status == PaymentStatus.AwaitingConfirmation);
        ApplyCancel(b);
        await _db.SaveChangesAsync();
        await _streaks.UpdateStreakAsync(b.CustomerId, b.StartTime);   // lịch hủy không còn được tính vào chuỗi
        await _drinks.OnCancelledAsync(b);                             // đơn nước: hoàn kho nếu chưa chuẩn bị. Tiền nước được ghi nhận hoàn cùng đơn

        TempData["Msg"] = hadPaid ? "Đã hủy lịch. Khoản thanh toán được ghi nhận hoàn tiền."
            : hadAwaiting ? "Đã hủy lịch. Nếu bạn đã chuyển tiền, hãy liên hệ chủ sân để được hoàn lại."
            : "Đã hủy lịch.";
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
        else if (status == BookingStatus.Confirmed)
        {
            // Xác nhận thủ công (ví dụ khách trả tiền mặt tại sân). Giao dịch đang chờ xác nhận coi như đã nhận tiền.
            b.Status = BookingStatus.Confirmed;
            foreach (var p in b.Payments.Where(p => p.Status == PaymentStatus.AwaitingConfirmation))
            {
                p.Status = PaymentStatus.Paid;
                p.PaidAt = DateTime.Now;
            }
        }
        await _db.SaveChangesAsync();
        if (status is BookingStatus.Confirmed or BookingStatus.Cancelled)
            await _streaks.UpdateStreakAsync(b.CustomerId, b.StartTime);   // xác nhận thì tính vào chuỗi, hủy thì loại ra
        if (status == BookingStatus.Confirmed) await _drinks.OnConfirmedAsync(b);
        else if (status == BookingStatus.Cancelled) await _drinks.OnCancelledAsync(b);
        return RedirectToAction(nameof(Manage));
    }

    // Hủy lịch: giao dịch đang chờ thì hủy, giao dịch đã trả thì ghi nhận hoàn tiền
    private static void ApplyCancel(Booking b)
    {
        b.Status = BookingStatus.Cancelled;
        foreach (var p in b.Payments)
        {
            if (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.AwaitingConfirmation) p.Status = PaymentStatus.Cancelled;
            else if (p.Status == PaymentStatus.Paid) p.Status = PaymentStatus.Refunded;
        }
    }
}
