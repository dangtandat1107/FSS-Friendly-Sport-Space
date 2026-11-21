using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Services;

// Quy tắc chuỗi tập luyện, mốc huy hiệu và lời động viên. Mọi con số nằm ở đây để chỉnh một chỗ
public static class StreakPolicy
{
    // Hạn giữ chuỗi: khoảng cách giữa 2 ngày đặt sân lớn hơn MaxGapDays thì mất chuỗi.
    // Khoảng cách 3 ngày (nghỉ 2 ngày) vẫn giữ chuỗi; khoảng cách 4 ngày (nghỉ liền 3 ngày) thì mất.
    public const int MaxGapDays = 3;

    public const int StarterDays = 3;       // 🌱 Người mới bắt đầu
    public const int WarriorDays = 7;       // ⚔️ Chiến binh
    public const int DisciplineDays = 14;   // 🛡️ Kỷ luật thép (tính theo chuỗi dài nhất)
    public const int LegendDays = 30;       // 👑 Huyền thoại

    // Lời động viên theo số ngày trong chuỗi (số ngày đã đặt sân, không tính ngày nghỉ xen giữa)
    public static string Message(int days) => days switch
    {
        <= 0 => "Hôm nay hãy đặt sân để bắt đầu chuỗi tập luyện mới nhé!",
        1 => "Bạn đã bắt đầu chuỗi tập luyện. Hãy quay lại trong vài ngày tới để giữ lửa nhé!",
        2 => "Chuỗi 2 ngày tập, thêm 1 ngày nữa là nhận huy hiệu đầu tiên!",
        < WarriorDays => $"Bạn giữ chuỗi {days} ngày tập, thói quen tốt đang hình thành. Tiếp tục nào!",
        < DisciplineDays => $"Bạn giữ chuỗi tập luyện {days} ngày vì sức khỏe rất tốt, cứ thế phát huy nhé!",
        < LegendDays => $"{days} ngày tập trong chuỗi, kỷ luật thật đáng nể! Còn {LegendDays - days} ngày nữa là chạm mốc 1 tháng.",
        _ => $"{days} ngày tập trong chuỗi, bạn đã vượt mốc 1 tháng. Đúng là huyền thoại!"
    };

    // Thông báo khi khách đang nghỉ (vẫn còn hạn giữ chuỗi). daysLeft: số ngày sau hôm nay mà đặt sân vẫn giữ được chuỗi
    public static (string Level, string Text)? GraceNotice(bool inGrace, int daysLeft)
    {
        if (!inGrace) return null;
        if (daysLeft <= 0) return ("danger", "🚨 Hôm nay là ngày cuối để giữ chuỗi. Hãy đặt sân hôm nay!");
        if (daysLeft == 1) return ("warning", "⚠️ Bạn còn 1 ngày để giữ chuỗi. Hãy đặt sân ngày mai nhé!");
        return ("info", $"Bạn đang trong ngày nghỉ hợp lý. Bạn còn {daysLeft} ngày để giữ chuỗi.");
    }
}

public static class BadgeToast
{
    // Ví dụ: "🎉 Bạn vừa nhận huy hiệu: ⚔️ Chiến binh!"
    public static string? For(IReadOnlyCollection<Badge> badges)
        => badges.Count == 0 ? null : "🎉 Bạn vừa nhận huy hiệu: " + string.Join(", ", badges.Select(b => $"{b.Icon} {b.Name}")) + "!";
}

public record StreakResult(
    int Current, int Longest, DateTime? LastDate, int MonthDays, int YearDays,
    int GraceDaysUsedThisMonth, bool InGrace, int GraceDaysLeft, HashSet<DateTime> RestDays);

// Hàm thuần (không đụng database) nên dễ viết unit test
public static class StreakCalculator
{
    /// <param name="playDays">Ngày của các buổi đã xác nhận. Chỉ tính đến hôm nay; lịch đặt trước cho ngày chưa tới sẽ được tính khi ngày đó tới.</param>
    /// <param name="excusedDays">Ngày khách báo sự cố y tế. Ngày này không phải ngày tập (không tăng chuỗi) nhưng được coi là "đang hồi phục",
    /// nên làm mới hạn giữ chuỗi thay vì bị tính là bỏ tập.</param>
    public static StreakResult Compute(IEnumerable<DateTime> playDays, IEnumerable<DateTime> excusedDays, DateTime today)
    {
        var t = today.Date;
        var play = playDays.Select(d => d.Date).Where(d => d <= t).ToHashSet();   // HashSet: nhiều lượt cùng ngày chỉ tính 1 ngày
        if (play.Count == 0) return new StreakResult(0, 0, null, 0, 0, 0, false, 0, new HashSet<DateTime>());
        var excused = excusedDays.Select(d => d.Date).Where(d => d <= t).ToHashSet();

        // Duyệt các ngày có hoạt động theo thứ tự. Khoảng cách tới ngày trước lớn hơn MaxGapDays thì chuỗi bắt đầu lại,
        // ngược lại chuỗi được giữ và các ngày nghỉ ở giữa được bỏ qua (không tăng, không giảm).
        var events = play.Union(excused).OrderBy(d => d).ToList();
        int run = 0, longest = 0;
        DateTime? prev = null;
        var forgiven = new List<DateTime>();   // các ngày nghỉ đã được bỏ qua nhờ còn hạn giữ chuỗi
        foreach (var d in events)
        {
            if (prev == null || (d - prev.Value).Days > StreakPolicy.MaxGapDays) run = 0;
            else for (var x = prev.Value.AddDays(1); x < d; x = x.AddDays(1)) forgiven.Add(x);

            if (play.Contains(d)) run++;       // chỉ ngày đặt sân mới làm chuỗi dài thêm
            longest = Math.Max(longest, run);
            prev = d;
        }

        // Chuỗi còn sống nếu từ hoạt động gần nhất đến nay chưa quá MaxGapDays ngày
        var end = events[^1];
        var sinceEnd = (t - end).Days;
        var alive = sinceEnd <= StreakPolicy.MaxGapDays;
        var current = alive ? run : 0;

        // Đang nghỉ nhưng còn hạn giữ chuỗi: hôm nay chưa đặt sân. Số ngày còn lại để giữ chuỗi tính từ ngày mai
        var inGrace = alive && current > 0 && sinceEnd >= 1;
        var graceLeft = inGrace ? StreakPolicy.MaxGapDays - sinceEnd : 0;

        // Ngày nghỉ hợp lý đã dùng: các ngày nghỉ đã bỏ qua, cộng các ngày đã nghỉ xong từ lần cuối tới hôm qua (hôm nay chưa tính)
        var rest = new List<DateTime>(forgiven);
        if (alive) for (var x = end.AddDays(1); x < t; x = x.AddDays(1)) rest.Add(x);
        var shown = new HashSet<DateTime>(rest);
        shown.UnionWith(excused);

        return new StreakResult(current, longest, play.Max(),
            play.Count(d => d.Year == t.Year && d.Month == t.Month),
            play.Count(d => d.Year == t.Year),
            rest.Count(d => d.Year == t.Year && d.Month == t.Month),
            inGrace, graceLeft, shown);
    }
}

public interface IStreakService
{
    /// <summary>Gọi mỗi khi lịch được xác nhận hoặc bị hủy. Tính lại thống kê và trao huy hiệu. Lỗi chỉ ghi log, không làm hỏng thao tác gọi nó.</summary>
    Task<UserStats?> UpdateStreakAsync(string userId, DateTime bookingDate);

    /// <summary>Thống kê đã được làm mới. Các tính năng khác (ví dụ ưu đãi nước uống theo chuỗi) đọc CurrentStreak qua hàm này, không đọc thẳng bảng UserStats.</summary>
    Task<UserStats> GetStatsAsync(string userId);

    /// <summary>Trạng thái chuỗi cho trang hồ sơ: chuỗi hiện tại, số ngày còn lại để giữ chuỗi, huy hiệu đã đạt, tiến độ, 14 ngày gần nhất. Đánh dấu huy hiệu mới là đã báo.</summary>
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
            // Lịch cho ngày chưa tới chưa tính vào chuỗi (tránh đặt trước nhiều ngày để lấy huy hiệu).
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
        var (stats, r, played) = await RefreshAsync(userId);

        var badges = await _db.Badges.AsNoTracking().OrderBy(b => b.RequiredStreakDays).ToListAsync();
        var owned = await _db.UserBadges.Where(u => u.UserId == userId).ToListAsync();
        var ownedById = owned.ToDictionary(u => u.BadgeId);

        var summary = new StreakSummary
        {
            Stats = stats,
            Message = StreakPolicy.Message(stats.CurrentStreak),
            InGrace = r.InGrace,
            GraceDaysLeft = r.GraceDaysLeft,
            Badges = badges.Select(b => new BadgeStatus(b, ownedById.TryGetValue(b.Id, out var ub) ? ub.EarnedAt : null)).ToList()
        };

        var notice = StreakPolicy.GraceNotice(r.InGrace, r.GraceDaysLeft);
        if (notice != null) { summary.GraceLevel = notice.Value.Level; summary.GraceText = notice.Value.Text; }

        // Huy hiệu kế tiếp chưa đạt và tiến độ đối với mốc đó. Huy hiệu theo chuỗi dài nhất (Kỷ luật thép) thì tính theo LongestStreak
        var next = badges.FirstOrDefault(b => !ownedById.ContainsKey(b.Id));
        if (next != null)
        {
            var basis = next.Code == "discipline" ? stats.LongestStreak : stats.CurrentStreak;
            summary.NextBadge = next;
            summary.DaysToNext = Math.Max(0, next.RequiredStreakDays - basis);
            summary.ProgressPercent = Math.Min(100, basis * 100 / Math.Max(1, next.RequiredStreakDays));
        }

        // 14 ngày gần nhất: ngày có buổi tập, ngày nghỉ hợp lý (không mất chuỗi), ngày không có gì
        var today = DateTime.Today;
        summary.Last14 = Enumerable.Range(0, 14).Select(i => today.AddDays(i - 13))
            .Select(d => new DayDot(d, played.Contains(d), r.RestDays.Contains(d))).ToList();

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
    // Lịch đã hủy không được tính, tức là ngày đó thành ngày nghỉ (còn hạn giữ chuỗi thì chuỗi vẫn giữ), không bị phạt thêm.
    private async Task<(UserStats Stats, StreakResult Result, HashSet<DateTime> Played)> RefreshAsync(string userId)
    {
        var today = DateTime.Today;
        var starts = await _db.Bookings.AsNoTracking()
            .Where(b => b.CustomerId == userId && b.Status == BookingStatus.Confirmed && b.StartTime < today.AddDays(1))
            .Select(b => b.StartTime).ToListAsync();

        // Báo sự cố y tế (trừ báo cáo bị từ chối) không bị tính là bỏ tập: ngày đó làm mới hạn giữ chuỗi
        var excused = await _db.Incidents.AsNoTracking()
            .Where(i => i.ReporterId == userId && i.Category == IncidentCategory.Medical && i.Status != IncidentStatus.Rejected)
            .Select(i => i.CreatedAt).ToListAsync();

        var r = StreakCalculator.Compute(starts, excused, today);

        var stats = await _db.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
        if (stats == null) { stats = new UserStats { UserId = userId }; _db.UserStats.Add(stats); }
        stats.CurrentStreak = r.Current;
        stats.LongestStreak = r.Longest;
        stats.LastBookingDate = r.LastDate;
        stats.TotalBookings = starts.Count;
        stats.MonthlyBookings = r.MonthDays;
        stats.YearlyBookings = r.YearDays;
        stats.GraceDaysUsedThisMonth = r.GraceDaysUsedThisMonth;
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
        return (stats, r, starts.Select(s => s.Date).ToHashSet());
    }
}
