using System.ComponentModel.DataAnnotations;
namespace SportBooking.Web.Models;

public enum SportType
{
    [Display(Name = "Cầu lông")] Badminton,
    [Display(Name = "Bóng đá")] Football,
    [Display(Name = "Tennis")] Tennis
}

public enum CourtStatus
{
    [Display(Name = "Hoạt động")] Active,
    [Display(Name = "Bảo trì")] Maintenance
}

public enum BookingStatus
{
    [Display(Name = "Chờ thanh toán")] Pending,
    [Display(Name = "Đã xác nhận")] Confirmed,
    [Display(Name = "Đã hủy")] Cancelled
}

public enum PaymentMethod
{
    [Display(Name = "Chuyển khoản ngân hàng")] BankTransfer,
    [Display(Name = "Ví MoMo")] Momo
}

public enum PaymentStatus
{
    [Display(Name = "Chờ thanh toán")] Pending,
    [Display(Name = "Đã thanh toán")] Paid,
    [Display(Name = "Thất bại")] Failed,
    [Display(Name = "Đã hủy")] Cancelled,
    [Display(Name = "Đã hoàn tiền")] Refunded
}
