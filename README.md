# FSS - Friendly Sport Space

Hệ thống đặt lịch sân thể thao (cầu lông, bóng đá, tennis, pickleball, bóng rổ) - Đồ án chuyên ngành Công nghệ phần mềm tích hợp AI, HUTECH.

> Trạng thái: **đang phát triển (đang ở tuần 5)**. Đã xong nghiệp vụ lõi và thanh toán chuyển khoản VietQR. Chưa có unit test, tài liệu thiết kế trong repo, các hạng mục mở rộng và phần AI.

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

Kế hoạch tuần 4: hoàn thiện ERD, vẽ Use Case (StarUML), unit test cho `BookingService`, thanh toán giả lập.

### Tuần 4 (2/11 - 8/11) - Thanh toán và nâng cấp giao diện
Đã làm trong tuần này:
- Thanh toán giả lập bằng chuyển khoản và MoMo, giữ chỗ 15 phút (lịch chờ thanh toán quá hạn thì giờ được trả về trống); thêm bảng `Payments`, cột `ExpiresAt` và migration
- Cấu hình tài khoản nhận tiền (User Secrets hoặc `.env`), tạo mã VietQR động tự điền số tiền và nội dung chuyển khoản
- Luồng xác nhận: khách bấm "đã chuyển khoản", chủ sân đối chiếu rồi xác nhận hoặc từ chối trong mục Duyệt lịch. Hệ thống không tự đối soát với ngân hàng
- Hủy lịch đã thanh toán thì giao dịch chuyển sang "Đã hoàn tiền" (chỉ ghi nhận trạng thái, không hoàn tiền thật)
- Nâng cấp giao diện, thêm môn pickleball và bóng rổ (nay có 5 môn), ảnh sân theo môn

Chưa làm so với kế hoạch tuần 4:
- Unit test cho `BookingService`: repo chưa có project test nào
- Hoàn thiện ERD và vẽ Use Case: chưa có file nào trong repo
- MoMo mới ở mức mô phỏng (mã QR tĩnh hoặc ảnh minh họa), chưa tích hợp cổng thanh toán MoMo

### Tuần 5 (9/11 - 15/11) - Hoàn thiện trải nghiệm và tính năng mới (đang làm)
Đã làm và đã commit:
- Ảnh minh họa cho từng môn; đổi ảnh đại diện ở trang hồ sơ (JPG, PNG, WEBP, tối đa 2 MB); cải thiện giao diện hồ sơ
- Loại sân và mặt sân theo từng môn, bộ lọc tìm sân nhiều bước, chủ sân tải ảnh riêng cho từng sân

Đã code, đang kiểm thử (chưa commit):
- Môn Bóng đá: bỏ bộ lọc và lựa chọn trong nhà/ngoài trời (luôn là sân ngoài trời)
- Đánh giá sân từ 1 đến 5 sao sau giờ chơi, điểm trung bình trên thẻ sân (bảng `Reviews`, migration `AddReviews`)
- Lịch đặt sân dạng lưới (hàng là sân, cột là khung giờ; xanh còn trống, đỏ đã đặt, vàng đang thanh toán), bấm ô xanh để đặt
- Toast khi đặt sân thành công kèm mã đơn FSS-XXXXX
- Giao diện tối, lưu lựa chọn trong `localStorage`

Còn lại:
- Unit test cho `BookingService` và điều kiện đánh giá (chuyển từ tuần 4)
- Hoàn thiện ERD, Use Case và đưa vào thư mục `docs/`
- Tiếp tục lộ trình đã chốt ở tuần 3: order nước kèm, khuyến mãi, nhân viên sân, ghi nhận sự cố, chuỗi tập luyện, rồi 2 chức năng AI

## Công nghệ
- ASP.NET Core 8 MVC, Razor Views, Bootstrap 5
- Entity Framework Core (Code-First, Migrations) + SQL Server
- ASP.NET Core Identity (phân quyền theo vai trò)
- Serilog (log có cấu trúc), health check `/health`
- JavaScript thuần cho lịch lưới, toast và giao diện tối (không dùng thư viện front-end nặng)
- Docker, Docker Compose

## Chức năng đã có
**Khách hàng**
- Đăng ký, đăng nhập, quên mật khẩu bằng mã xác nhận gửi qua email, đổi mật khẩu
- Trang hồ sơ cá nhân: sửa họ tên, số điện thoại, đổi ảnh đại diện, xem thống kê và lịch sắp tới
- Giao diện theo từng môn (màu và hình mặt sân riêng), tìm sân theo môn, loại sân, mặt sân, vị trí (trong nhà/ngoài trời, trừ bóng đá) và quận
- Xem lịch trống theo ngày, đặt sân. Lịch đã thanh toán chỉ hủy được trước giờ chơi ít nhất 2 giờ; lịch chưa thanh toán hủy lúc nào cũng được
- Thanh toán chuyển khoản qua mã VietQR (tự điền số tiền và nội dung), giữ chỗ 15 phút. Khách báo đã chuyển khoản, chủ sân đối chiếu tài khoản ngân hàng rồi xác nhận. MoMo mới ở mức mô phỏng
- Mới ở tuần 5, đang kiểm thử: lịch dạng lưới, đánh giá sân 1 đến 5 sao, toast khi đặt thành công, giao diện tối

**Chủ sân**
- Thêm, sửa sân (kèm loại sân, mặt sân, ảnh riêng), đổi trạng thái bảo trì
- Duyệt hoặc từ chối lịch đặt, xác nhận hoặc từ chối thanh toán (chỉ thấy dữ liệu sân của mình)

**Admin**
- Thống kê tổng quan (số người dùng, sân, lượt đặt, doanh thu đã xác nhận), danh sách người dùng

**Kỹ thuật**
- Chống đặt trùng giờ bằng transaction mức Serializable (`Services/BookingService.cs`)
- Quên mật khẩu: mã 6 chữ số, hết hạn sau vài phút, giới hạn số lần gửi và số lần nhập sai
- Mật khẩu được băm, mọi form POST có chống CSRF, kiểm tra quyền sở hữu dữ liệu

## Tiến độ so với yêu cầu đề cương
| Hạng mục | Trạng thái |
|---|---|
| Nghiệp vụ, 3 actor, xác thực và phân quyền | Đã có |
| CSDL quan hệ, quản trị | Đã có |
| Thống kê cơ bản (4 chỉ số tổng quan) | Đã có |
| Docker Compose một lệnh | Đã có (web + SQL Server) |
| Serilog, health check, README | Đã có |
| Thanh toán | Một phần: VietQR và chủ sân xác nhận thủ công. MoMo mới mô phỏng, chưa có cổng thanh toán thật, chưa tự đối soát |
| Đánh giá sân, lịch dạng lưới, toast, giao diện tối | Đã code, đang kiểm thử |
| Unit test | Chưa (repo chưa có project test) |
| Tài liệu thiết kế (ERD, Use Case) | Chưa đưa vào repo |
| Thống kê nâng cao (biểu đồ, theo thời gian, theo sân) | Chưa |
| Cache dữ liệu, hàng đợi, xử lý nền, thời gian thực, reverse proxy HTTPS | Chưa. `IMemoryCache` hiện chỉ dùng để giới hạn thử mã quên mật khẩu; giữ chỗ 15 phút được kiểm tra khi truy vấn, chưa có tiến trình nền dọn lịch hết hạn |
| Hạng mục mở rộng đã chốt ở tuần 3 (order nước, khuyến mãi, nhân viên sân, ghi nhận sự cố, chuỗi tập luyện) | Chưa |
| Phần AI (chatbot đặt sân, tư vấn môn thể thao, dự báo giờ cao điểm), lớp kiểm soát AI, bộ kiểm thử AI | Chưa |
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

Bạn cũng có thể tự đăng ký tài khoản Khách hàng hoặc Chủ sân ở trang Đăng ký. Tài khoản khách demo có sẵn 3 lịch đã qua (2 lịch đã đánh giá, 1 lịch chưa) để thử luồng đánh giá.

## Cấu hình gửi email (quên mật khẩu)
Dùng Gmail SMTP với **App Password** của Google (không dùng mật khẩu Gmail thật).

- Chạy local: dùng User Secrets (chuột phải project, *Manage User Secrets*):
  ```json
  { "Email": { "User": "your@gmail.com", "Password": "app-password-16-ky-tu" } }
  ```
- Chạy Docker: đặt `SMTP_USER` và `SMTP_PASSWORD` trong file `.env`.

Không commit mật khẩu hay file `.env` lên GitHub. Nếu chưa cấu hình, ở môi trường Development nội dung email (có mã xác nhận) sẽ được ghi ra log.

## Loại sân theo từng môn
Mỗi sân có loại sân, mặt sân và vị trí (trong nhà hoặc ngoài trời; riêng Bóng đá luôn là ngoài trời nên không có lựa chọn này). Các lựa chọn phụ thuộc vào môn:

| Môn | Loại sân | Mặt sân |
|---|---|---|
| Bóng đá | Sân 5, sân 7, sân 11 người | Cỏ nhân tạo, cỏ tự nhiên |
| Bóng rổ | Nửa sân (3x3), cả sân (5x5) | Sàn gỗ, sân cứng |
| Tennis | Không chia | Sân cứng, đất nện |
| Cầu lông | Không chia | Thảm nhựa PVC, sàn gỗ |
| Pickleball | Không chia (đánh đơn hay đôi đều dùng chung một loại sân) | Sân cứng, sàn gỗ |

Khách tìm sân theo từng bước: chọn môn, rồi chọn loại sân, mặt sân, trong nhà hoặc ngoài trời (trừ bóng đá). Chủ sân chọn các thuộc tính này và tải ảnh riêng cho từng sân khi thêm hoặc sửa sân (ảnh lưu tại `wwwroot/uploads/courts`, không có ảnh riêng thì dùng ảnh chung của môn).

## Ảnh môn thể thao và ảnh đại diện
- Mỗi môn có ảnh minh họa mặc định trong `wwwroot/images/sports`. Muốn dùng ảnh thật, đặt ảnh vào cùng thư mục với tên `photo-badminton.jpg`, `photo-football.jpg`, `photo-tennis.jpg`, `photo-pickleball.jpg`, `photo-basketball.jpg` (cũng nhận `.png`, `.webp`). Hệ thống tự ưu tiên ảnh thật. Chỉ dùng ảnh có bản quyền cho phép (ví dụ Unsplash, Pexels, Pixabay).
- Người dùng đổi ảnh đại diện ở trang hồ sơ (JPG, PNG, WEBP, tối đa 2 MB). Ảnh lưu tại `wwwroot/uploads/avatars` (đã nằm trong `.gitignore`), chạy Docker thì được giữ trong volume `avatars`.

## Cấu hình tài khoản nhận tiền (thanh toán)
Mặc định trang thanh toán hiển thị dữ liệu minh họa. Muốn thử với tài khoản thật, đặt trong User Secrets (chạy local) hoặc file `.env` (Docker), không ghi vào code:

```json
{ "Payment": { "Bank": { "BankName": "...", "BankBin": "...", "AccountNumber": "...", "AccountName": "..." } } }
```

`BankBin` là mã BIN của ngân hàng (ví dụ Vietcombank là 970436), dùng để tạo mã VietQR động. Hệ thống **không tự đối soát** tiền vào tài khoản: chủ sân đối chiếu và bấm xác nhận trong mục Duyệt lịch.

## Đánh giá sân và lịch dạng lưới
- **Đánh giá:** sau khi khung giờ của một lịch đã xác nhận kết thúc, khách vào "Lịch của tôi" bấm "Đánh giá" để chấm 1 đến 5 sao và để lại bình luận (không bắt buộc). Mỗi lịch chỉ đánh giá một lần. Dữ liệu lưu ở bảng `Reviews` (`Id, CustomerId, CourtId, BookingId, Rating, Comment, CreatedAt`), điểm trung bình hiện trên thẻ sân (ví dụ `4.8⭐`) và danh sách đánh giá hiện ở trang chi tiết sân.
- **Lịch dạng lưới:** menu "Lịch trống" (`/Courts/Calendar`) và trang chi tiết sân hiển thị bảng sân x khung giờ. Ô xanh là còn trống, đỏ là đã đặt, vàng là đang có người thanh toán (giữ chỗ tối đa 15 phút). Bấm ô xanh để mở hộp thoại đặt sân.
- Dữ liệu mẫu có sẵn 3 lịch đã qua của `customer@demo.com` (2 lịch đã có đánh giá, 1 lịch chưa) để thử ngay luồng đánh giá. Sau khi pull code, chạy migration `AddReviews` (tự chạy khi khởi động ứng dụng).

## Thông báo và giao diện tối
- **Toast:** đặt sân thành công thì góc phải trên hiện "✅ Đặt sân thành công! Mã đơn: FSS-00042", tự ẩn sau 4 giây (bấm để đóng sớm). Mã đơn là `FSS-` cộng Id lịch đặt 5 chữ số (thuộc tính `Booking.Code`, không thêm cột). Server đặt `TempData["Toast"]`, `wwwroot/js/site.js` hiển thị; trong JS gọi `showToast("nội dung")` để dùng cho chỗ khác.
- **Giao diện tối:** nút mặt trăng/mặt trời ở thanh trên cùng, lưu lựa chọn trong `localStorage` (khóa `fss-theme`), áp dụng cho cả khách hàng, chủ sân và admin. Màu nằm ở `wwwroot/css/dark.css`.
- **Bóng đá:** môn Bóng đá không có bộ lọc và lựa chọn trong nhà/ngoài trời (luôn là ngoài trời). Quy tắc nằm ở `CourtOptions.HasIndoorOption`.

## Cấu trúc thư mục
```
src/SportBooking.Web
├── Controllers/   Account, Profile, Home, Courts, Bookings, Payments, Reviews, Admin
├── Models/        Court, Booking, Payment, Review, ApplicationUser, Enums
├── Data/          AppDbContext, DbSeeder
├── Services/      BookingService, PaymentService, VietQrService, EmailService, AvatarService, ImageStore, AppClaimsFactory
├── Extensions/    CourtOptions, ReviewExtensions, SportInfo, EnumExtensions
├── ViewModels/    Các model cho form và trang
├── Views/         Razor Views (Shared/_CalendarGrid là lịch dạng lưới)
├── wwwroot/       css (site, courts, dark), js (site.js), images, uploads
└── Migrations/    EF Core migrations
```

## Quy ước commit
Conventional Commits: `feat:`, `fix:`, `docs:`, `test:`, `chore:`

## Thành viên
- Đặng Tấn Đạt
