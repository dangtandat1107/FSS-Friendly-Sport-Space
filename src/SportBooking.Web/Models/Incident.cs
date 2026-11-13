using System.ComponentModel.DataAnnotations.Schema;
namespace SportBooking.Web.Models;

// Một bảng cho cả hai luồng. IsRealTime = true: SOS ngay trong giờ chơi. IsRealTime = false: phản hồi, khiếu nại sau giờ chơi.
public class Incident
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
    public int CourtId { get; set; }
    public Court? Court { get; set; }
    public string ReporterId { get; set; } = "";
    public ApplicationUser? Reporter { get; set; }

    public bool IsRealTime { get; set; }
    public IncidentSource Source { get; set; } = IncidentSource.Customer;
    public IncidentCategory Category { get; set; }
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Reported;
    public ComplaintReason? Reason { get; set; }          // chỉ có ở luồng phản hồi

    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime SlaDeadline { get; set; }             // hạn phải tiếp nhận; quá hạn mà chưa ai tiếp nhận thì leo thang lên Admin
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? EscalatedAt { get; set; }

    public string? HandledById { get; set; }              // người tiếp nhận đầu tiên
    public ApplicationUser? HandledBy { get; set; }
    public string? ResolutionNote { get; set; }
    public string? OwnerResponse { get; set; }            // phản hồi và đề xuất bồi thường của chủ sân (luồng phản hồi)
    public CompensationType Compensation { get; set; } = CompensationType.None;

    // Nhiều người báo cùng một sự cố: các báo cáo sau trỏ về báo cáo đầu tiên, chỉ báo cáo đầu tiên được xử lý
    public int? DuplicateOfId { get; set; }
    public Incident? DuplicateOf { get; set; }
    public int ReportCount { get; set; } = 1;

    public bool IsVerified { get; set; }                  // đã được xử lý xong tức là báo đúng, dùng để cộng điểm uy tín

    public List<IncidentLog> Logs { get; set; } = new();

    // Chỉ có getter nên EF không tạo cột trong database
    [NotMapped] public string Code => $"INC-{Id:D5}";
    [NotMapped] public bool IsOpen => Status is IncidentStatus.Reported or IncidentStatus.Acknowledged
                                              or IncidentStatus.InProgress or IncidentStatus.Escalated;
}

// Nhật ký kiểm toán: ai làm gì, lúc nào. Chỉ thêm, không sửa hoặc xóa.
// Lưu sẵn tên và vai trò tại thời điểm làm để đối soát về sau dù tài khoản có đổi hoặc bị xóa.
public class IncidentLog
{
    public int Id { get; set; }
    public int IncidentId { get; set; }
    public Incident? Incident { get; set; }

    public string Action { get; set; } = "";
    public IncidentStatus? FromStatus { get; set; }
    public IncidentStatus? ToStatus { get; set; }
    public string? Note { get; set; }

    public string? ActorId { get; set; }                  // null nếu do hệ thống tự làm
    public string ActorName { get; set; } = "";
    public string ActorRole { get; set; } = "";           // Customer, Staff, Owner, Admin hoặc System
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
