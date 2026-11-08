using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

public class CourtsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IBookingService _bookings;
    public CourtsController(AppDbContext db, IBookingService bookings) { _db = db; _bookings = bookings; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Công khai ----------
    public async Task<IActionResult> Index(SportType? sport, string? district)
    {
        var q = _db.Courts.AsNoTracking().AsQueryable();
        if (sport != null) q = q.Where(c => c.SportType == sport);
        if (!string.IsNullOrWhiteSpace(district)) q = q.Where(c => c.District.Contains(district));
        ViewData["Sport"] = sport; ViewData["District"] = district;
        return View(await q.OrderBy(c => c.Name).ToListAsync());
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
    public async Task<IActionResult> Create(CourtFormVm vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);
        _db.Courts.Add(new Court
        {
            Name = vm.Name, SportType = vm.SportType, District = vm.District,
            Address = vm.Address, PricePerHour = vm.PricePerHour, OwnerId = UserId
        });
        await _db.SaveChangesAsync();
        TempData["Msg"] = "Đã thêm sân.";
        return RedirectToAction(nameof(Manage));
    }

    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Edit(int id)
    {
        var c = await FindManageable(id);
        if (c == null) return NotFound();
        return View("Form", new CourtFormVm { Id = c.Id, Name = c.Name, SportType = c.SportType, District = c.District, Address = c.Address, PricePerHour = c.PricePerHour });
    }

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CourtFormVm vm)
    {
        if (!ModelState.IsValid) return View("Form", vm);
        var c = await FindManageable(vm.Id);
        if (c == null) return NotFound();
        c.Name = vm.Name; c.SportType = vm.SportType; c.District = vm.District; c.Address = vm.Address; c.PricePerHour = vm.PricePerHour;
        await _db.SaveChangesAsync();
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
}
