using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Services;

// Mốc và lời động viên. Các mốc huy hiệu dùng chung cho DbSeeder để chỉ có một nơi định nghĩa
public static class StreakPolicy
{
    public const int StarterDays = 3;     // 🌱 Người mới bắt đầu
    public const int WarriorDays = 7;     // ⚔️ Chiến binh
    public const int LegendDays = 30;     // 👑 Huyền thoại

    public static string Message(int days) => days switch
    {
        <= 0 => "Hôm nay hãy đặt sân để bắt đầu chuỗi tập luyện mới nhé!",
        1 => "Bạn đã bắt đầu chuỗi tập luyện. Ngày mai quay lại để giữ lửa nhé!",
        2 => "Chuỗi 2 ngày liên tiếp, chỉ cần thêm 1 ngày nữa là nhận huy hiệu đầu tiên!",
        < WarriorDays => $"Bạn giữ chuỗi {days} ngày liên tiếp, thói quen tốt đang hình thành. Tiếp tục nào!",
        < 14 => $"Bạn giữ chuỗi tập luyện {days} ngày vì sức khỏe rất tốt, cứ thế phát huy nhé!",
        < LegendDays => $"{days} ngày liên tiếp, ý chí thật đáng nể! Còn {LegendDays - days} ngày nữa là chạm mốc 1 tháng.",
        _ => $"{days} ngày liên tiếp, bạn đã vượt mốc 1 tháng. Đúng là huyền thoại!"
    };
}

public static class BadgeToast
{
    // Ví dụ: "🎉 Bạn vừa nhận huy hiệu: ⚔️ Chiến binh!"
    public static string? For(IReadOnlyCollection<Badge> badges)
        => badges.Count == 0 ? null : "🎉 Bạn vừa nhận huy hiệu: " + string.Join(", ", badges.Select(b => $"{b.Icon} {b.Name}")) + "!";
}

public record StreakResult(int Current, int Longest, DateTime? LastDate, int MonthDays, int YearDays);

// Hàm thuần (không đụng database) nên dễ viết unit test
public static class StreakCalculator
{
    // playDays: ngày của các buổi đã xác nhận. Chỉ tính đến hôm nay; lịch đặt trước cho ngày chưa tới sẽ được tính khi ngày đó tới.
    public static StreakResult Compute(IEnumerable<DateTime> playDays, DateTime today)
    {
        var days = playDays.Select(d => d.Date).Where(d => d <= today.Date).Distinct().OrderBy(d => d).ToList();
        if (days.Count == 0) return new StreakResult(0, 0, null, 0, 0);

        // Duyệt các ngày theo thứ tự: ngày liền sau ngày trước thì chuỗi dài thêm 1, nếu không thì bắt đầu lại từ 1
        int run = 1, longest = 1;
        for (var i = 1; i < days.Count; i++)
        {
            run = (days[i] - days[i - 1]).Days == 1 ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }

        // Sau vòng lặp, run chính là độ dài chuỗi kết thúc ở ngày chơi gần nhất.
        // Chuỗi còn "sống" nếu ngày đó là hôm nay hoặc hôm qua (hôm nay vẫn còn thời gian để đặt tiếp); xa hơn thì đã đứt.
        var last = days[^1];
        var current = last >= today.Date.AddDays(-1) ? run : 0;

        return new StreakResult(current, longest, last,
            days.Count(d => d.Year == today.Year && d.Month == today.Month),
            days.Count(d => d.Year == today.Year));
    }
}

public interface IStreakService
{
    /// <summary>Gọi mỗi khi lịch được xác nhận hoặc bị hủy. Tính lại thống kê và trao huy hiệu. Lỗi chỉ ghi log, không làm hỏng thao tác gọi nó.</summary>
    Task<UserStats?> UpdateStreakAsync(string userId, DateTime bookingDate);

    /// <summary>Thống kê đã được làm mới. Các tính năng khác (ví dụ ưu đãi nước uống theo chuỗi) đọc CurrentStreak qua hàm này, không đọc thẳng bảng UserStats.</summary>
    Task<UserStats> GetStatsAsync(string userId);

    /// <summary>Dữ liệu cho trang hồ sơ: thống kê, lời động viên, tiến độ huy hiệu, 14 ngày gần nhất. Đánh dấu các huy hiệu mới là đã báo.</summary>
    Task<StreakSummary> GetSummaryAsync(string userId);
}

public class StreakService : IStreakService
{
    private readonly AppDbContext _db;
    private readonly ILogger<StreakService> _log;
    public StreakService(AppDbContext db, ILogger<StreakService> log) { _db = db; _log = log; }

    public async Task<UserStats?> UpdateStreakAsync(string userId, DateTime bookingDate)
    {
        try
        {
            // Lịch cho ngày chưa tới chưa tính vào chuỗi (tránh đặt trước 7 ngày liền để lấy huy hiệu).
            // Khi ngày đó tới, GetStatsAsync sẽ tính vào.
            if (bookingDate.Date > DateTime.Today) return null;
            return (await RefreshAsync(userId)).Stats;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Không cập nhật được chuỗi tập luyện của {User}", userId);
            return null;
        }
    }

    public async Task<UserStats> GetStatsAsync(string userId) => (await RefreshAsync(userId)).Stats;

    public async Task<StreakSummary> GetSummaryAsync(string userId)
    {
        var (stats, played) = await RefreshAsync(userId);

        var badges = await _db.Badges.AsNoTracking().OrderBy(b => b.RequiredStreakDays).ToListAsync();
        var owned = await _db.UserBadges.Where(u => u.UserId == userId).ToListAsync();
        var ownedById = owned.ToDictionary(u => u.BadgeId);

        var summary = new StreakSummary
        {
            Stats = stats,
            Message = StreakPolicy.Message(stats.CurrentStreak),
            Badges = badges.Select(b => new BadgeStatus(b, ownedById.TryGetValue(b.Id, out var ub) ? ub.EarnedAt : null)).ToList()
        };

        // Huy hiệu kế tiếp chưa đạt và tiến độ của chuỗi hiện tại đối với mốc đó
        var next = badges.FirstOrDefault(b => !ownedById.ContainsKey(b.Id));
        if (next != null)
        {
            summary.NextBadge = next;
            summary.DaysToNext = Math.Max(0, next.RequiredStreakDays - stats.CurrentStreak);
            summary.ProgressPercent = Math.Min(100, stats.CurrentStreak * 100 / Math.Max(1, next.RequiredStreakDays));
        }

        // 14 ngày gần nhất, ngày nào có buổi tập thì tô màu
        var today = DateTime.Today;
        summary.Last14 = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13))
            .Select(d => new DayDot(d, played.Contains(d))).ToList();

        // Huy hiệu mới đạt mà khách chưa được báo
        var fresh = owned.Where(u => !u.IsSeen).ToList();
        if (fresh.Count > 0)
        {
            summary.NewlyEarned = badges.Where(b => fresh.Any(u => u.BadgeId == b.Id)).ToList();
            foreach (var u in fresh) u.IsSeen = true;
            await _db.SaveChangesAsync();
        }
        return summary;
    }

    // Tính lại toàn bộ từ bảng Bookings (nguồn dữ liệu thật) rồi lưu vào UserStats và trao huy hiệu.
    // Nhờ vậy đúng cả khi các lịch được xác nhận không theo thứ tự ngày, hoặc có lịch bị hủy.
    private async Task<(UserStats Stats, HashSet<DateTime> PlayedDays)> RefreshAsync(string userId)
    {
        var today = DateTime.Today;
        var starts = await _db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == userId && b.Status == BookingStatus.Confirmed && b.StartTime < today.AddDays(1))
            .Select(b => b.StartTime).ToListAsync();
        var r = StreakCalculator.Compute(starts, today);

        var stats = await _db.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
        if (stats == null) { stats = new UserStats { UserId = userId }; _db.UserStats.Add(stats); }
        stats.CurrentStreak = r.Current;
        stats.LongestStreak = r.Longest;
        stats.LastBookingDate = r.LastDate;
        stats.TotalBookings = starts.Count;
        stats.MonthlyBookings = r.MonthDays;
        stats.YearlyBookings = r.YearDays;
        stats.UpdatedAt = DateTime.Now;

        // Trao huy hiệu khi chuỗi dài nhất chạm mốc. Đã trao thì không thu hồi
        var badges = await _db.Badges.AsNoTracking().ToListAsync();
        var ownedIds = await _db.UserBadges.Where(u => u.UserId == userId).Select(u => u.BadgeId).ToListAsync();
        foreach (var b in badges.Where(b => b.RequiredStreakDays <= r.Longest && !ownedIds.Contains(b.Id)))
            _db.UserBadges.Add(new UserBadge { UserId = userId, BadgeId = b.Id });

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // Hai yêu cầu cùng lúc cùng ghi một dòng (khóa chính hoặc chỉ mục duy nhất chặn): bỏ phần của mình, dùng dữ liệu đã có
            foreach (var e in _db.ChangeTracker.Entries().Where(e => e.Entity is UserStats or UserBadge).ToList())
                e.State = EntityState.Detached;
            stats = await _db.UserStats.AsNoTracking().FirstAsync(s => s.UserId == userId);
        }
        return (stats, starts.Select(s => s.Date).ToHashSet());
    }
}
