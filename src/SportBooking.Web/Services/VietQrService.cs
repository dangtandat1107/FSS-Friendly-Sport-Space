using System.Globalization;
using System.Text;
using QRCoder;
namespace SportBooking.Web.Services;

public interface IVietQrService
{
    /// <summary>Tạo chuỗi VietQR (chuẩn NAPAS 247, chuyển đến số tài khoản) kèm số tiền và nội dung.</summary>
    string BuildPayload(string bankBin, string accountNumber, decimal amount, string content);

    /// <summary>Vẽ chuỗi VietQR thành ảnh PNG, trả về dạng data URI để nhúng thẳng vào thẻ img.</summary>
    string RenderPngDataUri(string payload);
}

public class VietQrService : IVietQrService
{
    public string BuildPayload(string bankBin, string accountNumber, decimal amount, string content)
    {
        // Mỗi trường có dạng: mã (2 ký tự) + độ dài (2 chữ số) + giá trị
        static string F(string id, string value) => id + value.Length.ToString("00") + value;

        // Nội dung chuyển khoản: chỉ chữ, số, khoảng trắng, tối đa 25 ký tự
        var cleaned = new string(content.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch == ' ').ToArray());
        if (cleaned.Length > 25) cleaned = cleaned[..25];

        var beneficiary = F("00", bankBin.Trim()) + F("01", accountNumber.Trim());
        var merchant = F("00", "A000000727") + F("01", beneficiary) + F("02", "QRIBFTTA");

        var sb = new StringBuilder();
        sb.Append(F("00", "01"));          // phiên bản
        sb.Append(F("01", "12"));          // 12 = QR động (có số tiền)
        sb.Append(F("38", merchant));      // thông tin người nhận (NAPAS)
        sb.Append(F("53", "704"));         // tiền tệ VND
        if (amount > 0) sb.Append(F("54", decimal.Truncate(amount).ToString(CultureInfo.InvariantCulture)));
        sb.Append(F("58", "VN"));
        if (cleaned.Length > 0) sb.Append(F("62", F("08", cleaned)));   // nội dung chuyển khoản
        sb.Append("6304");                 // trường CRC, giá trị tính ngay sau đây
        sb.Append(Crc16(sb.ToString()));
        return sb.ToString();
    }

    public string RenderPngDataUri(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(8);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }

    // CRC-16/CCITT-FALSE: đa thức 0x1021, giá trị đầu 0xFFFF
    private static string Crc16(string s)
    {
        var crc = 0xFFFF;
        foreach (var b in Encoding.ASCII.GetBytes(s))
        {
            crc ^= b << 8;
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) & 0xFFFF : (crc << 1) & 0xFFFF;
        }
        return crc.ToString("X4");
    }
}
