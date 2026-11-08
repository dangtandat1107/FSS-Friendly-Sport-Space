using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) { _db = db; }

    public async Task<IActionResult> Index()
    {
        var courts = await _db.Courts.AsNoTracking()
            .Where(c => c.Status == CourtStatus.Active).OrderBy(c => c.Id).ToListAsync();

        // Gợi ý: lấy sân đầu tiên của mỗi môn trước, rồi mới tới các sân còn lại
        var featured = courts.GroupBy(c => c.SportType)
            .SelectMany(g => g.Select((c, rank) => new { c, rank }))
            .OrderBy(x => x.rank).ThenBy(x => x.c.Id)
            .Select(x => x.c).Take(6).ToList();

        return View(new HomeVm
        {
            Featured = featured,
            CountBySport = courts.GroupBy(c => c.SportType).ToDictionary(g => g.Key, g => g.Count())
        });
    }
}
