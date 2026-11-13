namespace SportBooking.Web.Models;

// Nhân viên sân: một tài khoản có vai trò "Staff" được giao phụ trách một hoặc nhiều sân (mỗi sân một dòng).
// Chủ sân tạo nhân viên cho sân của mình. Nhân viên chỉ thấy và xử lý sự cố của các sân được giao.
public class Staff
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser? User { get; set; }
    public int CourtId { get; set; }
    public Court? Court { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
