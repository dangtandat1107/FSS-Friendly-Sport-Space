using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
namespace SportBooking.Web.Extensions;

public static class ReviewExtensions
{
    // Gán điểm trung bình và số lượt đánh giá cho một danh sách sân bằng đúng 1 câu truy vấn
    public static async Task LoadRatingsAsync(this AppDbContext db, IReadOnlyCollection<Court> courts)
    {
        if (courts.Count == 0) return;
        var ids = courts.Select(c => c.Id).ToList();
        var stats = await db.Reviews.AsNoTracking()
            .Where(r => ids.Contains(r.CourtId))
            .GroupBy(r => r.CourtId)
            .Select(g => new { CourtId = g.Key, Avg = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToListAsync();
        var map = stats.ToDictionary(x => x.CourtId);
        foreach (var c in courts)
        {
            if (map.TryGetValue(c.Id, out var s)) { c.AvgRating = s.Avg; c.ReviewCount = s.Count; }
            else { c.AvgRating = null; c.ReviewCount = 0; }
        }
    }

    // Định dạng điểm kiểu "4.8⭐". Dùng InvariantCulture để luôn là dấu chấm, không phụ thuộc cấu hình máy
    public static string Stars(this double avg) => avg.ToString("0.0", CultureInfo.InvariantCulture) + "⭐";
}
