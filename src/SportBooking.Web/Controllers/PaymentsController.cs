using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SportBooking.Web.Data;
using SportBooking.Web.Models;
using SportBooking.Web.Services;
using SportBooking.Web.ViewModels;
namespace SportBooking.Web.Controllers;

[Authorize]
public class PaymentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPaymentService _svc;
    private readonly IVietQrService _qr;
    private readonly PaymentOptions _opt;
    private readonly IWebHostEnvironment _env;

    public PaymentsController(AppDbContext db, IPaymentService svc, IVietQrService qr,
        IOptions<PaymentOptions> opt, IWebHostEnvironment env)
    { _db = db; _svc = svc; _qr = qr; _opt = opt.Value; _env = env; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Khách hàng ----------

    // Bước 1: xem lại lịch đặt và chọn phương thức thanh toán
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Pay(int bookingId)
    {
        var b = await _db.Bookings.AsNoTracking().Include(x => x.Court).Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.CustomerId == UserId);
        if (b == null) return NotFound();

        var awaiting = b.Payments.FirstOrDefault(p => p.Status == PaymentStatus.AwaitingConfirmation);
        if (awaiting != null) return RedirectToAction(nameof(Gateway), new { id = awaiting.Id });

        if (b.Status != BookingStatus.Pending || b.IsExpired)
        {
            TempData["Err"] = "Lịch này không còn chờ thanh toán.";
            return RedirectToAction("My", "Bookings");
        }
        return View(new PayVm { Booking = b, MomoAvailable = !string.IsNullOrWhiteSpace(_opt.Momo.AccountNumber) });
    }

    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int bookingId, PaymentMethod method)
    {
        if (method == PaymentMethod.Momo && string.IsNullOrWhiteSpace(_opt.Momo.AccountNumber))
            method = PaymentMethod.BankTransfer;   // MoMo chưa cấu hình thì dùng chuyển khoản

        var r = await _svc.StartAsync(UserId, bookingId, method);
        if (!r.Success) { TempData["Err"] = r.Error; return RedirectToAction("My", "Bookings"); }
        return RedirectToAction(nameof(Gateway), new { id = r.Payment!.Id });
    }

    // Bước 2: hiện mã QR và thông tin chuyển khoản
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> Gateway(int id)
    {
        var p = await _db.Payments.AsNoTracking()
            .Include(x => x.Booking).ThenInclude(b => b!.Court)
            .FirstOrDefaultAsync(x => x.Id == id && x.Booking!.CustomerId == UserId);
        if (p == null) return NotFound();
        if (p.Status != PaymentStatus.Pending && p.Status != PaymentStatus.AwaitingConfirmation)
        {
            TempData["Err"] = "Giao dịch này đã được xử lý.";
            return RedirectToAction("My", "Bookings");
        }
        return View(BuildGatewayVm(p));
    }

    // Bước 3: khách báo đã chuyển khoản, chờ chủ sân xác nhận
    [Authorize(Roles = "Customer"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportTransfer(int paymentId)
    {
        var r = await _svc.ReportTransferAsync(UserId, paymentId);
        if (!r.Success) { TempData["Err"] = r.Error; return RedirectToAction("My", "Bookings"); }
        TempData["Msg"] = "Đã ghi nhận. Chủ sân sẽ kiểm tra và xác nhận khi nhận được tiền.";
        return RedirectToAction(nameof(Gateway), new { id = paymentId });
    }

    // ---------- Chủ sân / Admin ----------

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int paymentId)
    {
        var r = await _svc.ConfirmAsync(UserId, User.IsInRole("Admin"), paymentId);
        if (r.Success) TempData["Msg"] = "Đã xác nhận nhận tiền. Lịch đặt đã được xác nhận.";
        else TempData["Err"] = r.Error;
        return RedirectToAction("Manage", "Bookings");
    }

    [Authorize(Roles = "Owner,Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int paymentId)
    {
        var r = await _svc.RejectAsync(UserId, User.IsInRole("Admin"), paymentId);
        if (r.Success) TempData["Msg"] = $"Đã báo chưa nhận được tiền. Khách có thêm {PaymentService.RetryWindowMinutes} phút để thử lại.";
        else TempData["Err"] = r.Error;
        return RedirectToAction("Manage", "Bookings");
    }

    // ---------- Dựng dữ liệu cho trang QR ----------
    private GatewayVm BuildGatewayVm(Payment p)
    {
        var isMomo = p.Method == PaymentMethod.Momo;
        var acct = isMomo ? _opt.Momo : _opt.Bank;
        static string Or(string? v, string fallback) => string.IsNullOrWhiteSpace(v) ? fallback : v;

        var vm = new GatewayVm
        {
            Payment = p,
            IsDemo = string.IsNullOrWhiteSpace(acct.AccountNumber),
            BankName = Or(acct.BankName, isMomo ? "Ví MoMo" : "Ngân hàng minh họa"),
            AccountNo = Or(acct.AccountNumber, isMomo ? "0900 000 000" : "0123456789"),
            AccountName = Or(acct.AccountName, "FSS SPORT SPACE")
        };

        if (!isMomo && !string.IsNullOrWhiteSpace(acct.BankBin) && !string.IsNullOrWhiteSpace(acct.AccountNumber))
        {
            // VietQR động: đã điền sẵn số tiền và nội dung
            var payload = _qr.BuildPayload(acct.BankBin, acct.AccountNumber, p.Amount, p.TransactionCode);
            vm.QrDataUri = _qr.RenderPngDataUri(payload);
            vm.QrKind = QrKind.Dynamic;
        }
        else if (!string.IsNullOrWhiteSpace(acct.QrImage) && _env.WebRootFileProvider.GetFileInfo(acct.QrImage).Exists)
        {
            vm.QrImagePath = acct.QrImage;
            vm.QrKind = QrKind.Static;
        }
        return vm;
    }
}
