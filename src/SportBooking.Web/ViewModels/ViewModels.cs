using System.ComponentModel.DataAnnotations;
using SportBooking.Web.Models;
namespace SportBooking.Web.ViewModels;

public class LoginVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password)] [Display(Name = "Mật khẩu")] public string Password { get; set; } = "";
}

public class RegisterVm
{
    [Required, Display(Name = "Họ tên")] public string FullName { get; set; } = "";
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required, DataType(DataType.Password), MinLength(6)] [Display(Name = "Mật khẩu")] public string Password { get; set; } = "";
    [Required, Display(Name = "Tôi là")] public string Role { get; set; } = "Customer";
}

public class CourtFormVm
{
    public int Id { get; set; }
    [Required, Display(Name = "Tên sân")] public string Name { get; set; } = "";
    [Display(Name = "Môn")] public SportType SportType { get; set; }
    [Required, Display(Name = "Quận")] public string District { get; set; } = "";
    [Required, Display(Name = "Địa chỉ")] public string Address { get; set; } = "";
    [Range(1000, 10000000), Display(Name = "Giá / giờ (VNĐ)")] public decimal PricePerHour { get; set; } = 80000;
}

public class CourtDetailsVm
{
    public Court Court { get; set; } = null!;
    public DateTime Date { get; set; }
    public HashSet<int> BusyHours { get; set; } = new();
}

public class AdminDashboardVm
{
    public int Users { get; set; }
    public int Courts { get; set; }
    public int Bookings { get; set; }
    public decimal Revenue { get; set; }
    public List<(string Email, string FullName, string Roles)> UserList { get; set; } = new();
}

public class ForgotPasswordVm
{
    [Required(ErrorMessage = "Vui lòng nhập email"), EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = "";
}

public class ResetPasswordVm
{
    [Required, EmailAddress] public string Email { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập mã"), StringLength(6, MinimumLength = 6, ErrorMessage = "Mã gồm 6 chữ số")]
    [Display(Name = "Mã xác nhận")] public string Code { get; set; } = "";

    [Required, DataType(DataType.Password), MinLength(6)]
    [Display(Name = "Mật khẩu mới")] public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp")]
    [Display(Name = "Nhập lại mật khẩu mới")] public string ConfirmPassword { get; set; } = "";
}

public class UpdateProfileVm
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
    [Display(Name = "Họ tên")] public string FullName { get; set; } = "";

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")] public string? PhoneNumber { get; set; }
}

public class ChangePasswordVm
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại"), DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")] public string CurrentPassword { get; set; } = "";

    [Required, DataType(DataType.Password), MinLength(6)]
    [Display(Name = "Mật khẩu mới")] public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp")]
    [Display(Name = "Nhập lại mật khẩu mới")] public string ConfirmPassword { get; set; } = "";
}

public record StatItem(string Label, string Value);

public class ProfilePageVm
{
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Customer";
    public string FullName { get; set; } = "";
    public string ActiveTab { get; set; } = "info";
    public UpdateProfileVm Edit { get; set; } = new();
    public ChangePasswordVm ChangePassword { get; set; } = new();
    public List<StatItem> Stats { get; set; } = new();
    public List<Booking> Upcoming { get; set; } = new();
    public List<Court> MyCourts { get; set; } = new();
    public string? AvatarUrl { get; set; }

    public string RoleLabel => Role switch { "Admin" => "Quản trị viên", "Owner" => "Chủ sân", _ => "Khách hàng" };
    public string Initial => string.IsNullOrWhiteSpace(FullName) ? "?"
        : FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Last()[..1].ToUpper();
}
