# FSS - Friendly Sport Space

Hệ thống đặt lịch sân thể thao (cầu lông, bóng đá, tennis, pickleball, bóng rổ) - Đồ án chuyên ngành Công nghệ phần mềm tích hợp AI, HUTECH.

> Trạng thái: **đang phát triển**. Hiện đã hoàn thành phần nghiệp vụ lõi (Giai đoạn 1). Phần AI sẽ được bổ sung ở các giai đoạn sau.

## Tiến độ theo tuần

### Tuần 1 - Đăng ký và tìm hiểu
- Đăng ký đề tài: hệ thống đặt lịch sân thể thao tích hợp AI
- Tìm hiểu yêu cầu đề cương, chốt bộ công nghệ (ASP.NET Core MVC, EF Core, SQL Server, Docker)
- Xác định 3 actor (Khách hàng, Chủ sân, Admin) và hướng tích hợp AI (chatbot đặt sân, dự báo giờ cao điểm)

### Tuần 2 - Tạo repository và xây dựng cơ sở dữ liệu
Đã làm trong tuần này:
- Tạo repository GitHub, cấu hình `.gitignore`, quy ước Conventional Commits
- Dựng khung dự án ASP.NET Core 8 MVC, thiết kế CSDL Code-First (Court, Booking, ApplicationUser) và migration đầu tiên
- Xác thực và phân quyền 3 vai trò bằng ASP.NET Core Identity, có dữ liệu mẫu và tài khoản demo
- Nghiệp vụ đặt sân: tìm sân, xem lịch trống theo ngày, đặt sân, hủy lịch, chủ sân duyệt lịch, Admin xem thống kê
- Chống đặt trùng giờ bằng transaction mức Serializable
- Trang hồ sơ cá nhân, đổi mật khẩu, quên mật khẩu bằng mã xác nhận gửi qua Gmail
- Đổi tên thương hiệu thành FSS (Friendly Sport Space)

### Tuần 3 - Đóng gói, khảo sát và thiết kế
Đã làm trong tuần này:
- Đóng gói bằng Docker Compose (web + SQL Server), chạy thành công, có health check `/health` và Serilog
- Khảo sát các ứng dụng đặt sân tương tự (Playo, Play Space, Hi5Sport, Skedda, Playtomic) để bổ sung tính năng
- Chốt lộ trình 15 hạng mục từ dễ đến khó, mở rộng phạm vi: order nước kèm, khuyến mãi, nhân viên sân, ghi nhận sự cố, chuỗi tập luyện, 2 chức năng AI (chatbot đặt sân, tư vấn môn thể thao)
- Dựng bản nháp ERD bằng Case Studio (đang hoàn thiện kiểu dữ liệu và quan hệ)

Dự kiến tuần 4: hoàn thiện ERD, vẽ Use Case (StarUML), unit test cho `BookingService`, thanh toán giả lập.

## Công nghệ
- ASP.NET Core 8 MVC, Razor Views, Bootstrap 5
- Entity Framework Core (Code-First, Migrations) + SQL Server
- ASP.NET Core Identity (phân quyền theo vai trò)
- Serilog (log có cấu trúc), health check `/health`
- Docker, Docker Compose

## Chức năng đã có
**Khách hàng**
- Đăng ký, đăng nhập, quên mật khẩu bằng mã xác nhận gửi qua email, đổi mật khẩu
- Trang hồ sơ cá nhân: sửa họ tên, số điện thoại, xem thống kê và lịch sắp tới
- Giao diện theo từng môn (màu và hình mặt sân riêng), tìm sân theo môn và quận, xem lịch trống theo ngày, đặt sân, hủy lịch (trước giờ chơi ít nhất 2 giờ)
- Thanh toán chuyển khoản qua mã VietQR (tự điền số tiền và nội dung), giữ chỗ 15 phút. Khách báo đã chuyển khoản, chủ sân đối chiếu tài khoản ngân hàng rồi xác nhận

**Chủ sân**
- Thêm, sửa sân, đổi trạng thái bảo trì
- Duyệt hoặc từ chối lịch đặt (chỉ thấy dữ liệu sân của mình)

**Admin**
- Thống kê tổng quan, danh sách người dùng

**Kỹ thuật**
- Chống đặt trùng giờ bằng transaction mức Serializable (`Services/BookingService.cs`)
- Quên mật khẩu: mã 6 chữ số, hết hạn sau vài phút, giới hạn số lần gửi và số lần nhập sai
- Mật khẩu được băm, form có chống CSRF, kiểm tra quyền sở hữu dữ liệu

## Tiến độ so với yêu cầu đề cương
| Hạng mục | Trạng thái |
|---|---|
| Nghiệp vụ, 3 actor, xác thực và phân quyền | Đã có |
| CSDL quan hệ, quản trị, thống kê cơ bản | Đã có |
| Docker Compose một lệnh | Đã có (web + SQL Server) |
| Serilog, health check, README | Đã có |
| Unit test, thanh toán, thống kê nâng cao | Chưa |
| Cache, hàng đợi, xử lý nền, thời gian thực, reverse proxy HTTPS | Chưa |
| Phần AI (chatbot đặt sân, dự báo giờ cao điểm), lớp kiểm soát AI, bộ kiểm thử AI | Chưa |
| Triển khai public có HTTPS | Chưa |

## Chạy local bằng Visual Studio
Yêu cầu: Visual Studio với workload *ASP.NET and web development*, SQL Server (hoặc LocalDB).

1. Mở `src/SportBooking.Web/SportBooking.Web.csproj`.
2. Sửa chuỗi kết nối `ConnectionStrings:Default` trong `appsettings.json` cho đúng máy của bạn, ví dụ:
   ```
   Server=localhost;Database=SportBooking;Trusted_Connection=True;TrustServerCertificate=True
   ```
3. Nhấn F5. Ứng dụng tự áp dụng migration và tạo dữ liệu mẫu ở lần chạy đầu.

Nếu sửa Model, tạo migration mới trong Package Manager Console: `Add-Migration TenMigration`.

## Chạy bằng Docker (một lệnh)
```bash
cp .env.example .env     # điền SA_PASSWORD (và SMTP nếu cần gửi mail)
docker compose up --build
```
Mở http://localhost:8080. Health check: http://localhost:8080/health

## Tài khoản demo
| Vai trò | Email | Mật khẩu |
|---|---|---|
| Admin | admin@demo.com | Admin@123 |
| Chủ sân | owner@demo.com | Owner@123 |
| Khách hàng | customer@demo.com | Customer@123 |

Bạn cũng có thể tự đăng ký tài khoản Khách hàng hoặc Chủ sân ở trang Đăng ký.

## Cấu hình gửi email (quên mật khẩu)
Dùng Gmail SMTP với **App Password** của Google (không dùng mật khẩu Gmail thật).

- Chạy local: dùng User Secrets (chuột phải project, *Manage User Secrets*):
  ```json
  { "Email": { "User": "your@gmail.com", "Password": "app-password-16-ky-tu" } }
  ```
- Chạy Docker: đặt `SMTP_USER` và `SMTP_PASSWORD` trong file `.env`.

Không commit mật khẩu hay file `.env` lên GitHub. Nếu chưa cấu hình, ở môi trường Development nội dung email (có mã xác nhận) sẽ được ghi ra log.

## Ảnh môn thể thao và ảnh đại diện
- Mỗi môn có ảnh minh họa mặc định trong `wwwroot/images/sports`. Muốn dùng ảnh thật, đặt ảnh vào cùng thư mục với tên `photo-badminton.jpg`, `photo-football.jpg`, `photo-tennis.jpg`, `photo-pickleball.jpg`, `photo-basketball.jpg` (cũng nhận `.png`, `.webp`). Hệ thống tự ưu tiên ảnh thật. Chỉ dùng ảnh có bản quyền cho phép (ví dụ Unsplash, Pexels, Pixabay).
- Người dùng đổi ảnh đại diện ở trang hồ sơ (JPG, PNG, WEBP, tối đa 2 MB). Ảnh lưu tại `wwwroot/uploads/avatars` (đã nằm trong `.gitignore`), chạy Docker thì được giữ trong volume `avatars`.

## Cấu hình tài khoản nhận tiền (thanh toán)
Mặc định trang thanh toán hiển thị dữ liệu minh họa. Muốn thử với tài khoản thật, đặt trong User Secrets (chạy local) hoặc file `.env` (Docker), không ghi vào code:

```json
{ "Payment": { "Bank": { "BankName": "...", "BankBin": "...", "AccountNumber": "...", "AccountName": "..." } } }
```

`BankBin` là mã BIN của ngân hàng (ví dụ Vietcombank là 970436), dùng để tạo mã VietQR động. Hệ thống **không tự đối soát** tiền vào tài khoản: chủ sân đối chiếu và bấm xác nhận trong mục Duyệt lịch.

## Cấu trúc thư mục
```
src/SportBooking.Web
├── Controllers/   Account, Profile, Courts, Bookings, Admin
├── Models/        Court, Booking, ApplicationUser, Enums
├── Data/          AppDbContext, DbSeeder
├── Services/      BookingService, EmailService, AppClaimsFactory
├── ViewModels/    Các model cho form và trang
├── Views/         Razor Views
└── Migrations/    EF Core migrations
```

## Quy ước commit
Conventional Commits: `feat:`, `fix:`, `docs:`, `test:`, `chore:`

## Thành viên
- Đặng Tấn Đạt
