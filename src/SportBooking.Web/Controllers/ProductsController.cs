using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

// Chủ sân quản lý danh mục "Đồ uống & Phụ kiện" của sân mình và xem báo cáo doanh thu bán nước. Admin quản lý được tất cả sân.
[Authorize(Roles = "Owner,Admin")]
public class ProductsController : Controller
{
    private const string ImageFolder = "products";
    private const long ImageMaxBytes = 4 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IImageStore _images;
    public ProductsController(AppDbContext db, IImageStore images) { _db = db; _images = images; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // Sân thuộc quyền quản lý: sân của mình (Admin thì tất cả)
    private IQueryable<Court> MyCourts()
    {
        var q = _db.Courts.AsQueryable();
        return User.IsInRole("Admin") ? q : q.Where(c => c.OwnerId == UserId);
    }

    public async Task<IActionResult> Index()
    {
        var courts = await MyCourts().AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var ids = courts.Select(c => c.Id).ToList();
        var products = await _db.Products.AsNoTracking().Where(p => ids.Contains(p.CourtId))
            .OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
        return View(new ProductIndexVm
        {
            Courts = courts, Products = products,
            ImageUrls = products.Select(p => (p.Id, Url: _images.GetUrl(ImageFolder, p.Id.ToString())))
                .Where(x => x.Url != null).ToDictionary(x => x.Id, x => x.Url!)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? courtId)
    {
        await LoadCourtsAsync();
        return View("Form", new ProductFormVm { CourtId = courtId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Create(ProductFormVm vm)
    {
        // Chỉ được thêm vào sân của mình
        if (!await MyCourts().AnyAsync(c => c.Id == vm.CourtId)) ModelState.AddModelError(nameof(vm.CourtId), "Vui lòng chọn sân của bạn.");
        await ValidateImageAsync(vm);
        if (!ModelState.IsValid) { await LoadCourtsAsync(); return View("Form", vm); }

        var p = new Product
        {
            CourtId = vm.CourtId, Name = vm.Name.Trim(), Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim(),
            Price = vm.Price, Category = vm.Category, Stock = vm.Stock, IsActive = vm.IsActive
        };
        _db.Products.Add(p);
        await _db.SaveChangesAsync();
        if (vm.Image is { Length: > 0 }) await _images.SaveAsync(ImageFolder, p.Id.ToString(), vm.Image, ImageMaxBytes);   // đã kiểm tra ở trên

        TempData["Msg"] = $"Đã thêm sản phẩm {p.Name}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await FindMineAsync(id);
        if (p == null) return NotFound();
        return View("Form", new ProductFormVm
        {
            Id = p.Id, CourtId = p.CourtId, CourtName = p.Court?.Name, Name = p.Name, Description = p.Description, Price = p.Price,
            Category = p.Category, Stock = p.Stock, IsActive = p.IsActive, ImageUrl = _images.GetUrl(ImageFolder, p.Id.ToString())
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<IActionResult> Edit(int id, ProductFormVm vm)
    {
        var p = await FindMineAsync(id);
        if (p == null) return NotFound();
        await ValidateImageAsync(vm);
        if (!ModelState.IsValid)
        {
            vm.Id = p.Id; vm.CourtName = p.Court?.Name; vm.ImageUrl = _images.GetUrl(ImageFolder, p.Id.ToString());
            return View("Form", vm);
        }

        // Không cho đổi sân của sản phẩm đã tạo (các đơn cũ đã gắn với sân đó)
        p.Name = vm.Name.Trim(); p.Description = string.IsNullOrWhiteSpace(vm.Description) ? null : vm.Description.Trim();
        p.Price = vm.Price; p.Category = vm.Category; p.Stock = vm.Stock; p.IsActive = vm.IsActive;
        await _db.SaveChangesAsync();
        if (vm.Image is { Length: > 0 }) await _images.SaveAsync(ImageFolder, p.Id.ToString(), vm.Image, ImageMaxBytes);

        TempData["Msg"] = $"Đã cập nhật {p.Name}.";
        return RedirectToAction(nameof(Index));
    }

    // Sản phẩm chưa từng được đặt thì xóa hẳn; đã có đơn thì chỉ ẩn để lịch sử đơn hàng không bị mất
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await FindMineAsync(id);
        if (p == null) return NotFound();

        if (await _db.BookingItems.AnyAsync(i => i.ProductId == id))
        {
            p.IsActive = false;
            await _db.SaveChangesAsync();
            TempData["Msg"] = $"{p.Name} đã có trong đơn hàng nên được ẩn đi thay vì xóa, lịch sử đơn vẫn giữ nguyên.";
        }
        else
        {
            _db.Products.Remove(p);
            await _db.SaveChangesAsync();
            _images.Delete(ImageFolder, id.ToString());
            TempData["Msg"] = $"Đã xóa {p.Name}.";
        }
        return RedirectToAction(nameof(Index));
    }

    // ---------- Báo cáo doanh thu bán nước: theo ngày (14 ngày), tuần (8 tuần), tháng (6 tháng) ----------
    // Chỉ tính các lịch đã xác nhận thanh toán; quà tặng không tính vào doanh thu, chỉ đếm số lượng đã tặng
    public async Task<IActionResult> Report(string range = "day")
    {
        if (range is not ("day" or "week" or "month")) range = "day";
        var today = DateTime.Today;
        var thisMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var thisMonth = new DateTime(today.Year, today.Month, 1);

        // Danh sách các mốc thời gian cần hiện (kể cả mốc không có đơn thì hiện 0)
        var buckets = range switch
        {
            "week" => Enumerable.Range(0, 8).Select(i => thisMonday.AddDays(-7 * (7 - i))).ToList(),
            "month" => Enumerable.Range(0, 6).Select(i => thisMonth.AddMonths(i - 5)).ToList(),
            _ => Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13)).ToList()
        };
        DateTime KeyOf(DateTime d) => range switch
        {
            "week" => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7)),
            "month" => new DateTime(d.Year, d.Month, 1),
            _ => d.Date
        };
        string LabelOf(DateTime k) => range switch
        {
            "week" => $"{k:dd/MM} - {k.AddDays(6):dd/MM}",
            "month" => k.ToString("MM/yyyy"),
            _ => k.ToString("dd/MM")
        };

        var courtIds = await MyCourts().Select(c => c.Id).ToListAsync();
        var from = buckets[0];
        var rows = await _db.BookingItems.AsNoTracking()
            .Where(i => courtIds.Contains(i.Booking!.CourtId) && i.Booking.Status == BookingStatus.Confirmed
                     && i.Booking.StartTime >= from && i.Booking.StartTime < today.AddDays(1))
            .Select(i => new { i.Booking!.StartTime, i.ProductName, i.Quantity, i.TotalPrice, i.IsFreeGift })
            .ToListAsync();

        var byBucket = rows.GroupBy(r => KeyOf(r.StartTime)).ToDictionary(g => g.Key);
        var vm = new DrinkReportVm { Range = range };
        foreach (var k in buckets)
        {
            var g = byBucket.TryGetValue(k, out var grp) ? grp.ToList() : new();
            vm.Rows.Add(new DrinkReportRow(LabelOf(k),
                g.Where(r => !r.IsFreeGift).Sum(r => r.TotalPrice),
                g.Where(r => !r.IsFreeGift).Sum(r => r.Quantity),
                g.Where(r => r.IsFreeGift).Sum(r => r.Quantity)));
        }
        vm.TotalRevenue = vm.Rows.Sum(r => r.Revenue);
        vm.TotalItems = vm.Rows.Sum(r => r.Items);
        vm.TotalGifts = vm.Rows.Sum(r => r.Gifts);
        vm.Top = rows.Where(r => !r.IsFreeGift).GroupBy(r => r.ProductName)
            .Select(g => new TopProductRow(g.Key, g.Sum(r => r.Quantity), g.Sum(r => r.TotalPrice)))
            .OrderByDescending(t => t.Revenue).Take(5).ToList();
        return View(vm);
    }

    // ---------- Hàm phụ ----------
    private Task<Product?> FindMineAsync(int id)
    {
        var courtIds = MyCourts().Select(c => c.Id);   // truy vấn con, chỉ lấy sản phẩm thuộc sân của mình
        return _db.Products.Include(p => p.Court).FirstOrDefaultAsync(p => p.Id == id && courtIds.Contains(p.CourtId));
    }

    private async Task LoadCourtsAsync()
        => ViewBag.Courts = await MyCourts().AsNoTracking().OrderBy(c => c.Name).ToListAsync();

    private async Task ValidateImageAsync(ProductFormVm vm)
    {
        if (vm.Image is { Length: > 0 })
        {
            var (ok, error) = await _images.ValidateAsync(vm.Image, ImageMaxBytes);
            if (!ok) ModelState.AddModelError(nameof(vm.Image), error!);
        }
    }
}
