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

public class CourtsController : Controller
{
    private const string ImageFolder = "courts";
    private const long ImageMaxBytes = 4 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IBookingService _bookings;
    private readonly IImageStore _images;
    public CourtsController(AppDbContext db, IBookingService bookings, IImageStore images)
    { _db = db; _bookings = bookings; _images = images; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Công khai ----------
    public async Task<IActionResult> Index(SportType? sport, CourtFormat? format, CourtSurface? surface, bool? indoor, string? district)
    {
        var q = _db.Courts.AsNoTracking().AsQueryable();
        if (sport != null) q = q.Where(c => c.SportType == sport);
        if (format is { } f && f != CourtFormat.None) q = q.Where(c => c.CourtFormat == f);
        if (surface is { } sf && sf != CourtSurface.Unspecified) q = q.Where(c => c.Surface == sf);
        if (indoor != null) q = q.Where(c => c.IsIndoor == indoor);
        if (!string.IsNullOrWhiteSpace(district)) q = q.Where(c => c.District.Contains(district));

        var vm = new CourtListVm
        {
            Courts = await q.OrderBy(c => c.Name).ToListAsync(),
            Sport = sport, Format = format, Surface = surface, Indoor = indoor, District = district
        };

        // Số sân đang mở theo từng loại, để hiện cạnh nút lọc "Sân 5 người (2)"
        if (sport != null)
        {
            var counts = await _db.Courts.AsNoTracking()
                .Where(c => c.SportType == sport && c.Status == CourtStatus.Active)
                .GroupBy(c => c.CourtFormat).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
            vm.FormatCounts = counts.ToDictionary(x => x.Key, x => x.N);
        }
        return View(vm);
    }

    public async Task<IActionResult> Details(int id, DateTime? date)
    {
        var court = await _db.Courts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (court == null) return NotFound();

        // Không chọn ngày: mặc định là hôm nay, trừ khi hôm nay đã hết khung giờ cuối thì hiện ngày mai
        var lastSlotStart = DateTime.Today.AddHours(IBookingService.CloseHour - 1);
        var d = date?.Date ?? (DateTime.Now > lastSlotStart ? DateTime.Today.AddDays(1) : DateTime.Today);
        return View(new CourtDetailsVm { Court = court, Date = d, BusyHours = await _bookings.GetBusyHoursAsync(id, d) });
    }

    // ---------- Chủ sân / Admin ----------
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Manage()
    {
        var q = _db.Courts.AsNoTracking().AsQueryable();
        if (!User.IsInRole("Admin")) q = q.Where(c => c.OwnerId == UserId);
        return View(await q.OrderBy(c => c.Name).ToListAsync());
    }

    [Authorize(Roles = "Owner,Admin")]
    public IActionResult Create() => View("Form", new CourtFormVm());

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Create(CourtFormVm vm)
    {
        await ValidateFormAsync(vm);
        if (!ModelState.IsValid) return View("Form", vm);

        var court = new Court
        {
            Name = vm.Name.Trim(), SportType = vm.SportType, CourtFormat = vm.CourtFormat, Surface = vm.Surface,
            IsIndoor = vm.IsIndoor, District = vm.District.Trim(), Address = vm.Address.Trim(),
            PricePerHour = vm.PricePerHour, OwnerId = UserId
        };
        _db.Courts.Add(court);
        await _db.SaveChangesAsync();

        if (vm.Image is { Length: > 0 })
            await _images.SaveAsync(ImageFolder, court.Id.ToString(), vm.Image, ImageMaxBytes);   // ảnh đã được kiểm tra ở bước trên

        TempData["Msg"] = "Đã thêm sân.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var c = await FindManageable(id);
        if (c == null) return NotFound();
        return View("Form", new CourtFormVm
        {
            Id = c.Id, Name = c.Name, SportType = c.SportType, CourtFormat = c.CourtFormat, Surface = c.Surface,
            IsIndoor = c.IsIndoor, District = c.District, Address = c.Address, PricePerHour = c.PricePerHour,
            ImageUrl = _images.GetUrl(ImageFolder, c.Id.ToString())
        });
    }

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Edit(CourtFormVm vm)
    {
        var c = await FindManageable(vm.Id);
        if (c == null) return NotFound();

        await ValidateFormAsync(vm);
        if (!ModelState.IsValid)
        {
            vm.ImageUrl = _images.GetUrl(ImageFolder, c.Id.ToString());
            return View("Form", vm);
        }

        c.Name = vm.Name.Trim(); c.SportType = vm.SportType; c.CourtFormat = vm.CourtFormat; c.Surface = vm.Surface;
        c.IsIndoor = vm.IsIndoor; c.District = vm.District.Trim(); c.Address = vm.Address.Trim(); c.PricePerHour = vm.PricePerHour;
        await _db.SaveChangesAsync();

        if (vm.RemoveImage) _images.Delete(ImageFolder, c.Id.ToString());
        if (vm.Image is { Length: > 0 })
            await _images.SaveAsync(ImageFolder, c.Id.ToString(), vm.Image, ImageMaxBytes);

        TempData["Msg"] = "Đã cập nhật sân.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var c = await FindManageable(id);
        if (c == null) return NotFound();
        c.Status = c.Status == CourtStatus.Active ? CourtStatus.Maintenance : CourtStatus.Active;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Manage));
    }

    // Chủ sân chỉ sửa được sân của mình; Admin sửa được tất cả
    private Task<Court?> FindManageable(int id) =>
        _db.Courts.FirstOrDefaultAsync(c => c.Id == id && (User.IsInRole("Admin") || c.OwnerId == UserId));

    // Kiểm tra loại sân, mặt sân phù hợp với môn đã chọn và ảnh tải lên
    private async Task ValidateFormAsync(CourtFormVm vm)
    {
        var formats = CourtOptions.FormatsFor(vm.SportType);
        if (formats.Length > 0 && !formats.Contains(vm.CourtFormat))
            ModelState.AddModelError(nameof(vm.CourtFormat), "Vui lòng chọn loại sân.");
        if (formats.Length == 0) vm.CourtFormat = CourtFormat.None;

        if (!CourtOptions.SurfacesFor(vm.SportType).Contains(vm.Surface)) vm.Surface = CourtSurface.Unspecified;

        if (vm.Image is { Length: > 0 })
        {
            var (ok, error) = await _images.ValidateAsync(vm.Image, ImageMaxBytes);
            if (!ok) ModelState.AddModelError(nameof(vm.Image), error!);
        }
    }
}
