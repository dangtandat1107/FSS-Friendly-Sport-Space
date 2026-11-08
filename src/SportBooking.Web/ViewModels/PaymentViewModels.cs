using SportBooking.Web.Models;
namespace SportBooking.Web.ViewModels;

public enum QrKind { Dynamic, Static, Placeholder }

public class PayVm
{
    public Booking Booking { get; set; } = null!;
    public bool MomoAvailable { get; set; }
}

public class GatewayVm
{
    public Payment Payment { get; set; } = null!;
    public string BankName { get; set; } = "";
    public string AccountNo { get; set; } = "";
    public string AccountName { get; set; } = "";
    public bool IsDemo { get; set; }
    public QrKind QrKind { get; set; } = QrKind.Placeholder;
    public string? QrDataUri { get; set; }     // VietQR động (có sẵn số tiền, nội dung)
    public string? QrImagePath { get; set; }   // ảnh QR tĩnh dự phòng
}
