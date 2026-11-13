using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

[Authorize]
public class IncidentsController : Controller
{
    private const long PhotoMaxBytes = 4 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IIncidentService _incidents;
    private readonly IImageStore _images;

    public IncidentsController(AppDbContext db, IIncidentService incidents, IImageStore images)
    { _db = db; _incidents = incidents; _images = images; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Vai trò cao nhất của người dùng hiện tại: Admin, Owner, Staff hoặc Customer
    private IncidentActor Actor()
    {
        var role = User.IsInRole("Admin") ? "Admin" : User.IsInRole("Owner") ? "Owner" : User.IsInRole("Staff") ? "Staff" : "Customer";
        var name = User.FindFirst("FullName")?.Value;
        return new IncidentActor(UserId, string.IsNullOrWhiteSpace(name) ? User.Identity?.Name ?? "" : name, role);
    }

    // ================= Khách hàng: báo sự cố =================
    [Authorize(Roles = "Customer")]
    [HttpGet]
    public async Task<IActionResult> Report(int bookingId)
    {
        var vm = await BuildReportVmAsync(bookingId, new IncidentReportVm { BookingId = bookingId });
        if (vm == null) return NotFound();
        return View(vm);
    }

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(IncidentReportVm vm)
    {
        var shown = await BuildReportVmAsync(vm.BookingId, vm);
        if (shown == null) return NotFound();

        if (vm.Photo != null)
        {
            var (ok, error) = await _images.ValidateAsync(vm.Photo, PhotoMaxBytes);
            if (!ok) { ModelState.AddModelError("", error!); return View(vm); }
        }

        // Việc xác định luồng, mức độ, hạn xử lý và chống lạm dụng nằm trong IncidentService.CreateIncidentAsync
        var r = await _incidents.CreateIncidentAsync(UserId, new CreateIncidentRequest(vm.BookingId, vm.Category, vm.Reason, vm.Description ?? ""));
        if (!r.Ok) { ModelState.AddModelError("", r.Error!); return View(vm); }

        var incident = r.Incident!;
        if (vm.Photo != null) await _images.SaveAsync("incidents", incident.Id.ToString(), vm.Photo, PhotoMaxBytes);   // đã kiểm tra ở trên

        TempData["Toast"] = incident.IsRealTime
            ? $"🚨 Đã gửi SOS {incident.Code}. Nhân viên sân đang được thông báo."
            : $"✅ Đã gửi phản hồi {incident.Code}. Chủ sân sẽ xem xét và phản hồi bạn.";
        return RedirectToAction(nameof(Details), new { id = incident.Id });
    }

    // Điền thông tin hiển thị và xác định lịch đang ở trạng thái nào so với giờ hiện tại
    private async Task<IncidentReportVm?> BuildReportVmAsync(int bookingId, IncidentReportVm vm)
    {
        var b = await _db.Bookings.AsNoTracking().Include(x => x.Court)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.CustomerId == UserId && x.Status == BookingStatus.Confirmed);
        if (b == null) return null;

        var now = DateTime.Now;
        vm.BookingId = b.Id;
        vm.CourtName = b.Court?.Name;
        vm.TimeLabel = $"{b.StartTime:dd/MM/yyyy HH:mm} - {b.EndTime:HH:mm}";
        vm.Mode = now < b.StartTime ? ReportMode.NotYet
                : now < b.EndTime ? ReportMode.Sos
                : now <= b.EndTime.AddHours(IncidentPolicy.FeedbackWindowHours) ? ReportMode.Feedback
                : ReportMode.TooLate;

        var sent = await _db.Incidents.AsNoTracking().Where(i => i.BookingId == b.Id).Select(i => i.IsRealTime).ToListAsync();
        vm.HasSos = sent.Contains(true);
        vm.HasFeedback = sent.Contains(false);
        return vm;
    }

    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> My()
        => View(await _incidents.VisibleTo(Actor()).AsNoTracking().Include(i => i.Court)
            .OrderByDescending(i => i.CreatedAt).ToListAsync());

    // ================= Nhân viên, chủ sân, Admin: hàng đợi xử lý =================
    [Authorize(Roles = "Staff,Owner,Admin")]
    public async Task<IActionResult> Manage(string tab = "sos", bool all = false)
    {
        if (tab is not ("sos" or "feedback" or "escalated")) tab = "sos";
        var visible = _incidents.VisibleTo(Actor()).AsNoTracking().Where(i => i.DuplicateOfId == null);   // báo trùng đã gộp vào báo cáo gốc
        var open = visible.Where(i => i.Status == IncidentStatus.Reported || i.Status == IncidentStatus.Acknowledged
                                   || i.Status == IncidentStatus.InProgress || i.Status == IncidentStatus.Escalated);

        var q = tab switch
        {
            "escalated" => visible.Where(i => i.Status == IncidentStatus.Escalated),
            "feedback"  => visible.Where(i => !i.IsRealTime),
            _           => visible.Where(i => i.IsRealTime)
        };
        if (!all && tab != "escalated")
            // Mặc định chỉ hiện việc đang mở và việc đã xử lý xong nhưng chờ đóng
            q = q.Where(i => i.Status == IncidentStatus.Reported || i.Status == IncidentStatus.Acknowledged
                          || i.Status == IncidentStatus.InProgress || i.Status == IncidentStatus.Escalated || i.Status == IncidentStatus.Resolved);

        var vm = new IncidentManageVm
        {
            Tab = tab, ShowAll = all,
            Items = await q.Include(i => i.Court).Include(i => i.Reporter)
                .OrderByDescending(i => i.Severity).ThenBy(i => i.SlaDeadline).Take(100).ToListAsync(),
            OpenSos = await open.CountAsync(i => i.IsRealTime),
            OpenFeedback = await open.CountAsync(i => !i.IsRealTime),
            EscalatedCount = await visible.CountAsync(i => i.Status == IncidentStatus.Escalated)
        };
        return View(vm);
    }

    // ================= Chi tiết (khách xem của mình, nhân viên xem sân mình phụ trách) =================
    public async Task<IActionResult> Details(int id)
    {
        var inc = await _incidents.VisibleTo(Actor()).AsNoTracking()
            .Include(i => i.Court).Include(i => i.Reporter).Include(i => i.HandledBy).Include(i => i.DuplicateOf)
            .Include(i => i.Logs.OrderBy(l => l.CreatedAt))
            .FirstOrDefaultAsync(i => i.Id == id);
        if (inc == null) return NotFound();
        return View(inc);
    }

    [Authorize(Roles = "Staff,Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, IncidentStatus to, string? note)
    {
        var (ok, error) = await _incidents.ChangeStatusAsync(Actor(), id, to, note);
        if (ok) TempData["Toast"] = $"✅ Đã chuyển sang \"{to.DisplayName()}\"";
        else TempData["Err"] = error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int id, string? response, CompensationType compensation)
    {
        var (ok, error) = await _incidents.RespondAsync(Actor(), id, response ?? "", compensation);
        if (ok) TempData["Toast"] = "✅ Đã gửi phản hồi cho khách";
        else TempData["Err"] = error;
        return RedirectToAction(nameof(Details), new { id });
    }

    // ================= Thông báo gần thời gian thực =================
    // Trang của nhân viên, chủ sân, Admin gọi hàm này mỗi 15 giây (js/site.js) và hiện toast cho sự cố mới.
    // Nhân viên và chủ sân nhận SOS chưa ai tiếp nhận. Admin nhận sự cố đã bị leo thang.
    [Authorize(Roles = "Staff,Owner,Admin")]
    public async Task<IActionResult> Poll()
    {
        Response.Headers.CacheControl = "no-store";
        var actor = Actor();
        var q = _incidents.VisibleTo(actor).AsNoTracking().Include(i => i.Court).Where(i => i.DuplicateOfId == null);
        q = actor.Role == "Admin"
            ? q.Where(i => i.Status == IncidentStatus.Escalated)
            : q.Where(i => i.IsRealTime && i.Status == IncidentStatus.Reported);

        var items = await q.OrderByDescending(i => i.CreatedAt).Take(5).ToListAsync();
        return Json(items.Select(i => new
        {
            id = i.Id,
            text = (actor.Role == "Admin" ? "⚠️ Quá hạn " : "🚨 SOS ") + $"{i.Code} tại {i.Court?.Name}: {i.Category.DisplayName()}",
            url = Url.Action(nameof(Details), new { id = i.Id })
        }));
    }
}
