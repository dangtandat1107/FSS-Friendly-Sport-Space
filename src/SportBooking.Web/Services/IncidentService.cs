using System.Net;
using Microsoft.EntityFrameworkCore;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
namespace SportBooking.Web.Services;

public record CreateIncidentRequest(int BookingId, IncidentCategory? Category, ComplaintReason? Reason, string Description);
public record IncidentResult(bool Ok, string? Error, Incident? Incident);

// Người đang thao tác, dùng để lọc dữ liệu được xem, kiểm tra quyền và ghi nhật ký. Role: Customer, Staff, Owner, Admin hoặc System
public record IncidentActor(string UserId, string Name, string Role)
{
    public static readonly IncidentActor Auto = new("", "Hệ thống", "System");
}

// Mọi con số nghiệp vụ của tính năng nằm ở đây để chỉnh một chỗ
public static class IncidentPolicy
{
    public const int FeedbackWindowHours = 48;   // sau khi hết giờ chơi, khách còn 48 giờ để gửi phản hồi
    public const int MinTrustScore = 50;         // dưới mức này thì không gửi được báo cáo (trừ SOS y tế)
    public const int MaxTicketsPerDay = 3;       // số báo cáo tối đa của một khách trong 24 giờ
    public const int RejectPenalty = 10;         // trừ điểm uy tín khi báo cáo bị từ chối
    public const int VerifiedBonus = 2;          // cộng điểm uy tín khi báo cáo được xử lý xong (báo đúng)

    // Mức độ và hạn tiếp nhận (SLA) theo luồng và loại sự cố
    public static (IncidentSeverity Severity, TimeSpan Sla) For(bool isRealTime, IncidentCategory category) => (isRealTime, category) switch
    {
        (true, IncidentCategory.Medical or IncidentCategory.Security) => (IncidentSeverity.Critical, TimeSpan.FromMinutes(5)),
        (true, _)                                                      => (IncidentSeverity.High, TimeSpan.FromMinutes(15)),
        (false, IncidentCategory.Medical or IncidentCategory.Security) => (IncidentSeverity.Medium, TimeSpan.FromHours(24)),
        (false, IncidentCategory.Facility)                             => (IncidentSeverity.Medium, TimeSpan.FromHours(48)),
        _                                                              => (IncidentSeverity.Low, TimeSpan.FromHours(72))
    };

    // Luồng phản hồi: khách chọn lý do, hệ thống suy ra loại sự cố
    public static IncidentCategory CategoryOf(ComplaintReason reason) => reason switch
    {
        ComplaintReason.LateSymptoms => IncidentCategory.Medical,
        ComplaintReason.NotAsDescribed => IncidentCategory.Facility,
        ComplaintReason.LostItem => IncidentCategory.Security,
        _ => IncidentCategory.Operational
    };
}

public interface IIncidentService
{
    Task<IncidentResult> CreateIncidentAsync(string reporterId, CreateIncidentRequest req);
    IQueryable<Incident> VisibleTo(IncidentActor actor);
    Task<(bool Ok, string? Error)> ChangeStatusAsync(IncidentActor actor, int incidentId, IncidentStatus to, string? note);
    Task<(bool Ok, string? Error)> RespondAsync(IncidentActor actor, int incidentId, string response, CompensationType compensation);
    Task<int> EscalateOverdueAsync();
}

public class IncidentService : IIncidentService
{
    private readonly AppDbContext _db;
    private readonly IAppEmailSender _email;
    private readonly ILogger<IncidentService> _log;

    public IncidentService(AppDbContext db, IAppEmailSender email, ILogger<IncidentService> log)
    { _db = db; _email = email; _log = log; }

    // ================= Tạo báo cáo =================
    public async Task<IncidentResult> CreateIncidentAsync(string reporterId, CreateIncidentRequest req)
    {
        var now = DateTime.Now;
        var description = (req.Description ?? "").Trim();
        if (description.Length < 10) return Fail("Vui lòng mô tả ít nhất 10 ký tự để nhân viên hiểu rõ tình huống.");
        if (description.Length > 1000) return Fail("Mô tả tối đa 1000 ký tự.");

        // Chỉ báo cho lịch của chính mình và đã xác nhận thanh toán
        var booking = await _db.Bookings.Include(b => b.Court)
            .FirstOrDefaultAsync(b => b.Id == req.BookingId && b.CustomerId == reporterId);
        if (booking == null) return Fail("Không tìm thấy lịch đặt.");
        if (booking.Status != BookingStatus.Confirmed) return Fail("Chỉ báo sự cố cho lịch đã xác nhận thanh toán.");

        // Tự phát hiện luồng bằng cách so giờ hiện tại với khung giờ đặt (không tin dữ liệu client gửi lên)
        bool isRealTime;
        if (now >= booking.StartTime && now < booking.EndTime) isRealTime = true;                       // đang trong giờ chơi: SOS
        else if (now < booking.StartTime) return Fail("Chưa đến giờ chơi nên chưa thể báo sự cố.");
        else if (now <= booking.EndTime.AddHours(IncidentPolicy.FeedbackWindowHours)) isRealTime = false; // vừa kết thúc: phản hồi
        else return Fail($"Đã quá {IncidentPolicy.FeedbackWindowHours} giờ kể từ khi hết giờ chơi nên hệ thống không nhận phản hồi nữa. Vui lòng liên hệ trực tiếp chủ sân.");

        IncidentCategory category;
        if (isRealTime)
        {
            if (req.Category == null) return Fail("Vui lòng chọn loại sự cố.");
            category = req.Category.Value;
        }
        else
        {
            if (req.Reason == null) return Fail("Vui lòng chọn lý do khiếu nại.");
            category = IncidentPolicy.CategoryOf(req.Reason.Value);
        }

        // Chống lạm dụng. SOS y tế không bao giờ bị chặn vì liên quan đến tính mạng
        var reporter = await _db.Users.FirstAsync(u => u.Id == reporterId);
        var lifeSafety = isRealTime && category == IncidentCategory.Medical;
        if (!lifeSafety)
        {
            if (reporter.TrustScore < IncidentPolicy.MinTrustScore)
                return Fail("Điểm uy tín của bạn đang thấp nên tạm thời chưa gửi được báo cáo. Vui lòng liên hệ trực tiếp chủ sân.");
            var since = now.AddHours(-24);
            var sent = await _db.Incidents.CountAsync(i => i.ReporterId == reporterId && i.CreatedAt >= since);
            if (sent >= IncidentPolicy.MaxTicketsPerDay)
                return Fail($"Bạn đã gửi {IncidentPolicy.MaxTicketsPerDay} báo cáo trong 24 giờ qua. Vui lòng thử lại sau.");
        }

        // Mỗi lịch tối đa 2 báo cáo: 1 SOS và 1 phản hồi (chỉ mục duy nhất trong DB chặn thêm một lớp)
        if (await _db.Incidents.AnyAsync(i => i.BookingId == booking.Id && i.IsRealTime == isRealTime))
            return Fail(isRealTime ? "Bạn đã gửi SOS cho lịch này rồi." : "Bạn đã gửi phản hồi cho lịch này rồi. Mỗi lịch chỉ có 1 SOS và 1 phản hồi.");

        var (severity, sla) = IncidentPolicy.For(isRealTime, category);

        // Báo trùng: chỉ gộp sự cố cơ sở vật chất (lỗi chung của sân). Y tế, an ninh, mất đồ, thanh toán là việc riêng
        // của từng người nên không bao giờ gộp, tránh bỏ sót người bị nạn.
        Incident? parent = null;
        if (category == IncidentCategory.Facility)
        {
            var windowStart = now - (isRealTime ? TimeSpan.FromMinutes(60) : TimeSpan.FromHours(24));
            parent = await _db.Incidents
                .Where(i => i.CourtId == booking.CourtId && i.IsRealTime == isRealTime && i.Category == category
                         && i.DuplicateOfId == null && i.CreatedAt >= windowStart
                         && (i.Status == IncidentStatus.Reported || i.Status == IncidentStatus.Acknowledged
                          || i.Status == IncidentStatus.InProgress || i.Status == IncidentStatus.Escalated))
                .OrderBy(i => i.CreatedAt).FirstOrDefaultAsync();
        }

        var actor = new IncidentActor(reporterId, reporter.FullName, "Customer");
        var incident = new Incident
        {
            BookingId = booking.Id, CourtId = booking.CourtId, ReporterId = reporterId,
            IsRealTime = isRealTime, Source = IncidentSource.Customer,
            Category = category, Severity = severity, Status = IncidentStatus.Reported,
            Reason = isRealTime ? null : req.Reason,
            Description = description, CreatedAt = now, SlaDeadline = now + sla,
            DuplicateOfId = parent?.Id
        };
        incident.Logs.Add(Log(actor, parent == null ? "Tạo báo cáo" : "Tạo báo cáo (gộp vào báo cáo trước đó)", null, IncidentStatus.Reported,
            isRealTime ? "SOS trong giờ chơi" : "Phản hồi sau giờ chơi"));

        if (parent != null)
        {
            parent.ReportCount++;
            if (parent.ReportCount >= 3 && parent.Severity < IncidentSeverity.High) parent.Severity = IncidentSeverity.High;   // nhiều người cùng báo thì nâng mức
            parent.Logs.Add(Log(IncidentActor.Auto, "Có thêm người báo trùng", null, null, $"Tổng {parent.ReportCount} người báo"));
        }
        _db.Incidents.Add(incident);

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Fail("Báo cáo của bạn đã được gửi trước đó."); }

        // Thông báo: SOS thì nhân viên và chủ sân thấy ngay qua thông báo trên màn hình (xem Incidents/Poll);
        // phản hồi thì gửi email cho chủ sân, không báo khẩn. Báo trùng không gửi lại để khỏi làm phiền.
        if (parent == null && !isRealTime) await NotifyOwnerByEmailAsync(booking.Court!.OwnerId, incident, booking.Court.Name);

        return new IncidentResult(true, null, incident);
    }

    // ================= Phạm vi được xem =================
    // Khách chỉ thấy báo cáo của mình, nhân viên chỉ thấy sân được giao, chủ sân thấy sân của mình, Admin thấy tất cả
    public IQueryable<Incident> VisibleTo(IncidentActor actor)
    {
        var q = _db.Incidents.AsQueryable();
        var uid = actor.UserId;
        return actor.Role switch
        {
            "Admin" => q,
            "Owner" => q.Where(i => i.Court!.OwnerId == uid),
            "Staff" => q.Where(i => _db.Staffs.Any(s => s.UserId == uid && s.IsActive && s.CourtId == i.CourtId)),
            _ => q.Where(i => i.ReporterId == uid)
        };
    }

    // ================= State machine =================
    // Escalated chỉ do hệ thống đặt (EscalateOverdueAsync) nên không có trong danh sách chuyển tay
    private static readonly Dictionary<IncidentStatus, IncidentStatus[]> Transitions = new()
    {
        [IncidentStatus.Reported]     = new[] { IncidentStatus.Acknowledged, IncidentStatus.Rejected },
        [IncidentStatus.Escalated]    = new[] { IncidentStatus.Acknowledged, IncidentStatus.InProgress, IncidentStatus.Resolved, IncidentStatus.Rejected },
        [IncidentStatus.Acknowledged] = new[] { IncidentStatus.InProgress, IncidentStatus.Resolved, IncidentStatus.Rejected },
        [IncidentStatus.InProgress]   = new[] { IncidentStatus.Resolved },
        [IncidentStatus.Resolved]     = new[] { IncidentStatus.Closed },
        [IncidentStatus.Closed]       = Array.Empty<IncidentStatus>(),
        [IncidentStatus.Rejected]     = Array.Empty<IncidentStatus>()
    };

    public static IncidentStatus[] NextStatuses(IncidentStatus current, string role)
        => Transitions[current].Where(t => t is not (IncidentStatus.Rejected or IncidentStatus.Closed) || role is "Owner" or "Admin").ToArray();

    public async Task<(bool Ok, string? Error)> ChangeStatusAsync(IncidentActor actor, int incidentId, IncidentStatus to, string? note)
    {
        if (actor.Role is not ("Staff" or "Owner" or "Admin")) return (false, "Bạn không có quyền xử lý sự cố.");

        var inc = await VisibleTo(actor).Include(i => i.Logs).FirstOrDefaultAsync(i => i.Id == incidentId);
        if (inc == null) return (false, "Không tìm thấy sự cố hoặc bạn không phụ trách sân này.");
        if (inc.DuplicateOfId != null) return (false, "Báo cáo này đã được gộp vào một báo cáo khác, hãy xử lý báo cáo gốc.");
        if (!NextStatuses(inc.Status, actor.Role).Contains(to))
            return (false, $"Không thể chuyển từ \"{inc.Status.DisplayName()}\" sang \"{to.DisplayName()}\".");

        note = note?.Trim();
        if (to is IncidentStatus.Resolved or IncidentStatus.Rejected && (note == null || note.Length < 5))
            return (false, to == IncidentStatus.Resolved ? "Vui lòng ghi chú cách đã xử lý (ít nhất 5 ký tự)." : "Vui lòng ghi rõ lý do từ chối (ít nhất 5 ký tự).");
        if (to == IncidentStatus.Closed && !inc.IsRealTime && string.IsNullOrWhiteSpace(inc.OwnerResponse))
            return (false, "Phản hồi sau giờ chơi cần có phản hồi của chủ sân trước khi đóng.");

        var now = DateTime.Now;
        var previous = inc.Status;
        inc.Status = to;
        switch (to)
        {
            case IncidentStatus.Acknowledged:
                inc.AcknowledgedAt = now; inc.HandledById ??= actor.UserId; break;
            case IncidentStatus.InProgress:
                inc.AcknowledgedAt ??= now; inc.HandledById ??= actor.UserId; break;
            case IncidentStatus.Resolved:
                inc.AcknowledgedAt ??= now; inc.HandledById ??= actor.UserId;
                inc.ResolvedAt = now; inc.ResolutionNote = note; inc.IsVerified = true; break;
            case IncidentStatus.Closed:
                inc.ClosedAt = now; break;
            case IncidentStatus.Rejected:
                inc.ClosedAt = now; inc.ResolutionNote = note; break;
        }
        inc.Logs.Add(Log(actor, "Chuyển trạng thái", previous, to, note));

        // Các báo cáo trùng đi theo báo cáo gốc
        var children = await _db.Incidents.Include(i => i.Logs).Where(i => i.DuplicateOfId == inc.Id).ToListAsync();
        foreach (var c in children)
        {
            if (to == IncidentStatus.Resolved)
            {
                c.Status = IncidentStatus.Resolved; c.ResolvedAt = now; c.ResolutionNote = note; c.IsVerified = true;
                c.Logs.Add(Log(actor, "Xử lý xong theo báo cáo gốc", IncidentStatus.Reported, IncidentStatus.Resolved, note));
            }
            else if (to == IncidentStatus.Closed && c.Status == IncidentStatus.Resolved)
            {
                c.Status = IncidentStatus.Closed; c.ClosedAt = now;
                c.Logs.Add(Log(actor, "Đóng theo báo cáo gốc", IncidentStatus.Resolved, IncidentStatus.Closed, null));
            }
            else if (to == IncidentStatus.Rejected)
            {
                // Báo cáo gốc bị từ chối chưa có nghĩa những người báo trùng sai: tách ra xử lý riêng, tính lại hạn tiếp nhận
                c.DuplicateOfId = null;
                c.SlaDeadline = now + IncidentPolicy.For(c.IsRealTime, c.Category).Sla;
                c.Logs.Add(Log(IncidentActor.Auto, "Tách khỏi báo cáo gốc bị từ chối, chờ xử lý riêng", null, null, null));
            }
        }

        // Điểm uy tín: báo đúng (đã xử lý xong) thì cộng, bị từ chối thì trừ
        if (to == IncidentStatus.Resolved)
        {
            var ids = children.Select(c => c.ReporterId).Append(inc.ReporterId).Distinct().ToList();
            await AdjustTrustAsync(ids, IncidentPolicy.VerifiedBonus);
        }
        else if (to == IncidentStatus.Rejected)
            await AdjustTrustAsync(new[] { inc.ReporterId }, -IncidentPolicy.RejectPenalty);

        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RespondAsync(IncidentActor actor, int incidentId, string response, CompensationType compensation)
    {
        if (actor.Role is not ("Owner" or "Admin")) return (false, "Chỉ chủ sân hoặc Admin được phản hồi và đề xuất bồi thường.");
        response = (response ?? "").Trim();
        if (response.Length < 5) return (false, "Vui lòng nhập nội dung phản hồi (ít nhất 5 ký tự).");
        if (response.Length > 1000) return (false, "Phản hồi tối đa 1000 ký tự.");

        var inc = await VisibleTo(actor).Include(i => i.Logs).Include(i => i.Reporter).Include(i => i.Court)
            .FirstOrDefaultAsync(i => i.Id == incidentId);
        if (inc == null) return (false, "Không tìm thấy sự cố.");
        if (inc.IsRealTime) return (false, "Chỉ phản hồi bồi thường cho phản hồi sau giờ chơi. SOS xử lý bằng trạng thái.");
        if (inc.DuplicateOfId != null) return (false, "Báo cáo này đã được gộp vào một báo cáo khác.");
        if (inc.Status is IncidentStatus.Closed or IncidentStatus.Rejected) return (false, "Báo cáo đã đóng.");

        inc.OwnerResponse = response;
        inc.Compensation = compensation;
        inc.Logs.Add(Log(actor, "Chủ sân phản hồi", null, null, $"{compensation.DisplayName()}: {response}"));
        await _db.SaveChangesAsync();

        // Báo cho khách biết (gửi lỗi thì thôi, không làm hỏng thao tác)
        if (!string.IsNullOrWhiteSpace(inc.Reporter?.Email))
            await SafeSendAsync(inc.Reporter.Email!, $"[FSS] Chủ sân đã phản hồi báo cáo {inc.Code}",
                $"<p>Chủ sân <b>{WebUtility.HtmlEncode(inc.Court?.Name)}</b> đã phản hồi báo cáo {inc.Code} của bạn.</p>" +
                $"<p>Đề xuất: <b>{compensation.DisplayName()}</b></p><p>{WebUtility.HtmlEncode(response)}</p>");
        return (true, null);
    }

    // ================= Leo thang khi quá hạn =================
    // Báo cáo vẫn ở "Mới báo" khi quá hạn tiếp nhận (SlaDeadline) thì chuyển lên Admin. Do IncidentEscalationWorker gọi mỗi 30 giây
    public async Task<int> EscalateOverdueAsync()
    {
        var now = DateTime.Now;
        var overdue = await _db.Incidents.Include(i => i.Logs).Include(i => i.Court)
            .Where(i => i.Status == IncidentStatus.Reported && i.DuplicateOfId == null && i.SlaDeadline < now)
            .ToListAsync();
        if (overdue.Count == 0) return 0;

        foreach (var i in overdue)
        {
            i.Status = IncidentStatus.Escalated;
            i.EscalatedAt = now;
            i.Logs.Add(Log(IncidentActor.Auto, "Quá hạn tiếp nhận, tự động leo thang lên Admin",
                IncidentStatus.Reported, IncidentStatus.Escalated, $"Hạn tiếp nhận là {i.SlaDeadline:HH:mm dd/MM}"));
        }
        await _db.SaveChangesAsync();

        var adminEmails = await (from ur in _db.UserRoles
                                 join r in _db.Roles on ur.RoleId equals r.Id
                                 join u in _db.Users on ur.UserId equals u.Id
                                 where r.Name == "Admin" && u.Email != null
                                 select u.Email!).ToListAsync();
        var lines = string.Join("", overdue.Select(i =>
            $"<li>{i.Code} ({(i.IsRealTime ? "SOS" : "Phản hồi")}, {i.Severity.DisplayName()}) tại {WebUtility.HtmlEncode(i.Court?.Name)}</li>"));
        foreach (var to in adminEmails)
            await SafeSendAsync(to, $"[FSS] {overdue.Count} sự cố quá hạn tiếp nhận", $"<p>Các sự cố sau chưa có ai tiếp nhận đúng hạn:</p><ul>{lines}</ul>");

        return overdue.Count;
    }

    // ================= Hàm phụ =================
    private static IncidentResult Fail(string error) => new(false, error, null);

    private static IncidentLog Log(IncidentActor actor, string action, IncidentStatus? fromStatus, IncidentStatus? toStatus, string? note) => new()
    {
        Action = action, FromStatus = fromStatus, ToStatus = toStatus, Note = note,
        ActorId = string.IsNullOrEmpty(actor.UserId) ? null : actor.UserId, ActorName = actor.Name, ActorRole = actor.Role,
        CreatedAt = DateTime.Now
    };

    private async Task AdjustTrustAsync(IReadOnlyCollection<string> userIds, int delta)
    {
        var users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
        foreach (var u in users) u.TrustScore = Math.Clamp(u.TrustScore + delta, 0, 100);
    }

    private async Task NotifyOwnerByEmailAsync(string ownerId, Incident i, string courtName)
    {
        var email = await _db.Users.Where(u => u.Id == ownerId).Select(u => u.Email).FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(email)) return;
        await SafeSendAsync(email, $"[FSS] Phản hồi mới {i.Code} tại {courtName}",
            $"<p>Khách gửi phản hồi sau giờ chơi tại <b>{WebUtility.HtmlEncode(courtName)}</b>.</p>" +
            $"<p>Mức độ: <b>{i.Severity.DisplayName()}</b>. Hạn phản hồi: <b>{i.SlaDeadline:HH:mm dd/MM/yyyy}</b>.</p>" +
            $"<p>{WebUtility.HtmlEncode(i.Description)}</p><p>Đăng nhập FSS, mục Sự cố, để phản hồi và đề xuất bồi thường.</p>");
    }

    // Gửi email lỗi không được làm hỏng nghiệp vụ chính
    private async Task SafeSendAsync(string to, string subject, string html)
    {
        try { await _email.SendAsync(to, subject, html); }
        catch (Exception ex) { _log.LogWarning(ex, "Không gửi được email sự cố tới {To}", to); }
    }
}
