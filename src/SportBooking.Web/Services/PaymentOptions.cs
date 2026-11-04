namespace SportBooking.Web.Services;

// Thông tin tài khoản nhận tiền. Giá trị thật được đặt trong User Secrets hoặc biến môi trường,
// KHÔNG ghi vào code để không bị đưa lên GitHub. Để trống thì trang thanh toán hiện dữ liệu minh họa.
public class PaymentAccountOptions
{
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string QrImage { get; set; } = "";   // đường dẫn trong wwwroot, ví dụ /payment/bank-qr.png
}

public class PaymentOptions
{
    public PaymentAccountOptions Bank { get; set; } = new();
    public PaymentAccountOptions Momo { get; set; } = new();
}
