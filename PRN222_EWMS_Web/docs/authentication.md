# Đăng nhập và quản trị tài khoản

## Đăng nhập bằng email và Google

- Form đăng nhập dùng email và mật khẩu. Tài khoản chưa có email cần được Admin bổ sung email trước khi dùng cách đăng nhập này. Khi nâng cấp database hiện có, chạy `docs/sql/remove-user-username.sql` trước khi khởi động phiên bản ứng dụng này.
- Admin phải nhập email duy nhất khi tạo tài khoản mới. Google chỉ đăng nhập được khi email đã xác minh của Google khớp một tài khoản EWMS đang `ACTIVE`; lần đầu hệ thống tự lưu `sub` vào `GoogleSubject`, những lần sau tra theo `sub`. Không tự tạo tài khoản/role/kho mới. Nếu tài khoản đã gắn với Google khác, hệ thống từ chối.
- Tự ghép bằng email chỉ áp dụng khi Google quản lý địa chỉ đó: `@gmail.com` hoặc Google Workspace có `hd` khớp miền email. Với Google Account dùng email ngoài Google, cần phương thức xác nhận quyền sở hữu email bổ sung; hệ thống hiện từ chối tự ghép để tránh chiếm tài khoản.
- Database cần cột `dbo.[User].GoogleSubject varchar(255) NULL` và unique filtered index `UX_User_GoogleSubject`. Giá trị là `sub` lấy từ endpoint userinfo của Google; không nhập thủ công.
- Tạo OAuth Client loại **Web application** trong Google Cloud. Thêm redirect URI `https://localhost:7012/signin-google` khi chạy profile `https`, hoặc URI HTTPS tương ứng khi triển khai. Cấu hình `Authentication__Google__ClientId` và `Authentication__Google__ClientSecret` bằng biến môi trường hoặc secret store; không đưa secret vào repository. Chạy bằng `dotnet run --launch-profile https`. Khi chưa cấu hình, nút Google sẽ báo lỗi cấu hình.
- Với Visual Studio ở chế độ Development, dự án đã bật .NET User Secrets. Nhấp phải project `PRN222_EWMS_Web` → **Manage User Secrets** rồi nhập `Authentication:Google:ClientId`, `Authentication:Google:ClientSecret` và các khóa `Smtp` trong JSON cục bộ. File secrets nằm ngoài repo; mỗi máy cấu hình một lần.
- Tài khoản bị khóa/ngừng hoạt động không đăng nhập được bằng cả hai cách.
- Khi email của tài khoản EWMS thay đổi, `GoogleSubject` cũ được xóa và các phiên gắn Google cũ bị từ chối; lần đăng nhập Google sau phải khớp email mới và đáp ứng điều kiện Google quản lý địa chỉ đó. Cookie phát hành trước phiên bản này sẽ cần đăng nhập lại một lần.

## Quên mật khẩu

- `/Authentication/ForgotPassword`: nhận email và luôn trả thông báo chung, kể cả khi tài khoản không tồn tại. Tài khoản `ACTIVE` nhận thư chứa link HTTPS đặt lại mật khẩu.
- Link có hiệu lực 1 phút; token được bảo vệ bằng ASP.NET Core Data Protection, gắn với `UserID` và phiên bản mật khẩu hiện tại. Token đời trước bị vô hiệu khi chuyển sang purpose `v2`. Sau khi đặt lại hoặc đổi mật khẩu, link cũ không dùng lại được và cookie đăng nhập cũ bị từ chối.
- Cấu hình `PasswordReset__BaseUrl` là URL HTTPS công khai của ứng dụng (trong Development mặc định `https://localhost:7012`). Cấu hình SMTP qua `Smtp__Host`, `Smtp__Port`, `Smtp__Username`, `Smtp__Password`, `Smtp__From` ở biến môi trường hoặc secret store. SMTP phải hỗ trợ STARTTLS (thường cổng 587). Không đưa mật khẩu SMTP vào Git. Nếu chưa cấu hình, form báo chức năng gửi email chưa sẵn sàng.
- Khi chạy nhiều instance, các instance cần dùng chung kho Data Protection keys để xác nhận link và cookie. Lỗi gửi email được ghi vào log server, không tiết lộ tài khoản nào tồn tại trên form.
- Các màn quên/đặt lại mật khẩu dùng cùng layout hai cột và form của login. Khi SQL Server không trả lời kịp ở bước kiểm tra link hoặc lưu mật khẩu, trang hiển thị trạng thái tạm thời không khả dụng (HTTP 503) thay vì stack trace; hai thao tác này có giới hạn chờ 5 giây. Cần kiểm tra khóa/giao dịch SQL và các trigger trên bảng `User` nếu lỗi lặp lại.
- Tra cứu tài khoản khi đăng nhập email hoặc Google cũng có giới hạn chờ 5 giây và báo lỗi tạm thời thay vì trang exception. Timeout SQL không có nghĩa mật khẩu sai; cần thử lại và điều tra DB nếu xảy ra lặp lại.

Ví dụ chạy cục bộ với máy chủ SMTP hỗ trợ STARTTLS (điền thông tin thực của nhà cung cấp, không commit các lệnh có secret):

```powershell
$env:Smtp__Host = 'smtp.example.com'
$env:Smtp__Port = '587'
$env:Smtp__Username = 'sender@example.com'
$env:Smtp__Password = 'SMTP_PASSWORD'
$env:Smtp__From = 'sender@example.com'
dotnet run --launch-profile https
```

- `/Authentication/Login`: đăng nhập bằng email/mật khẩu hoặc Google có email đã được Admin tạo trong EWMS.
- Giao diện login theo bố cục hai cột: giới thiệu kho bên trái, form trắng bên phải, logo 📦 hiện tại và nút con mắt. Style nằm trong `wwwroot/css/login.css`, chỉ áp dụng cho login; trang 403 tiếp tục dùng layout gọn. Trên điện thoại form chuyển thành một cột. Không có ghi chú chính sách mật khẩu, số ngày ghi nhớ hay hướng dẫn phụ cuối form.
- Bấm vùng tài khoản cuối sidebar để mở menu `Profile` và `Đăng xuất`. Đăng xuất gửi POST kèm antiforgery token.
- `/Account/Profile`: xem tài khoản, sửa họ tên/email/số điện thoại và đổi mật khẩu. Mỗi người chỉ cập nhật tài khoản lấy từ cookie đăng nhập; không có ID tài khoản trong form. Username và role chỉ được xem. Kho làm việc chỉ hiển thị đối với `WAREHOUSE_STAFF`.
- Đổi mật khẩu yêu cầu mật khẩu hiện tại, mật khẩu mới dài 8–128 ký tự, khác mật khẩu cũ và xác nhận khớp. Sau khi lưu, cấp lại cookie với phiên bản mật khẩu mới cho phiên hiện tại, giữ trạng thái ghi nhớ đăng nhập và hạn sử dụng. Chuyển về Profile với thông báo thành công, không yêu cầu đăng nhập lại. Các cookie khác mang phiên bản mật khẩu cũ bị từ chối ở request tiếp theo.
- Tất cả ô mật khẩu ở login, tạo tài khoản và Profile có nút con mắt hiện/ẩn riêng, có thể dùng bằng bàn phím.
- `/User`: danh sách tài khoản; `/User/Create`: tạo tài khoản. Chỉ `ADMIN` được truy cập cả GET và POST.
- Các trang MVC mặc định yêu cầu đăng nhập. Tài khoản `LOCKED` hoặc `INACTIVE` không đăng nhập được; cookie được kiểm tra lại trạng thái, role và phiên bản mật khẩu mỗi request. Tên hiển thị được cập nhật theo DB. Cookie tạo trước khi bổ sung phiên bản mật khẩu sẽ yêu cầu đăng nhập lại một lần.
- Admin chọn một trong 5 role ngoài `ADMIN` từ DB. Giao diện không hiển thị `ADMIN`, service từ chối request tạo thêm `ADMIN`. Chỉ `WAREHOUSE_STAFF` được chọn và bắt buộc có kho đang hoạt động. Chuyển sang role khác sẽ xóa lựa chọn kho và khóa ô kho; server cũng lưu `WarehouseId = NULL` cho các role khác. Các module nghiệp vụ còn lại chưa được triển khai trong đợt này; menu của chúng giữ theo vai trò hiện có.

## Kết nối DB

Ứng dụng sử dụng `ConnectionStrings:MyCnn`. Điều chỉnh server/database theo SQL Server của bạn. SQL đính kèm chọn database `EWMS`, trong khi cấu hình dự án hiện dùng `WMSDB`; hãy dùng đúng database đã import. Ứng dụng không tự chạy script hoặc migration.

## Admin đầu tiên

SQL seed dùng `REPLACE_WITH_ASPNET_IDENTITY_HASH`, không phải mật khẩu đăng nhập. Không thể đăng nhập các tài khoản đó trước khi có hash hợp lệ.

Nếu chưa có Admin hoạt động với hash đã thiết lập, bật bootstrap bằng biến môi trường trong PowerShell:

```powershell
$env:BootstrapAdmin__Enabled = 'true'
$env:BootstrapAdmin__Username = 'admin'
$secret = Read-Host 'Mật khẩu Admin đầu tiên (12–128 ký tự)' -AsSecureString
$env:BootstrapAdmin__Password = [System.Net.NetworkCredential]::new('', $secret).Password
dotnet run
```

Bootstrap chỉ tạo Admin đầu tiên khi chưa có tài khoản Admin, hoặc thay placeholder của đúng tài khoản Admin được chỉ định. Nó không tạo thêm Admin khi đã có Admin khác, kể cả tài khoản bị khóa hoặc ngừng hoạt động, và không thay mật khẩu tài khoản đã có hash. Kiểm tra và lưu nằm trong transaction `Serializable` để tránh tạo trùng khi nhiều phiên ứng dụng cùng khởi động. Nếu Admin hiện có đã có hash, hãy đăng nhập bằng mật khẩu tương ứng.

Sau khi khởi tạo, dừng ứng dụng và xóa biến trước khi chạy lại:

```powershell
Remove-Item Env:BootstrapAdmin__Enabled, Env:BootstrapAdmin__Username, Env:BootstrapAdmin__Password
dotnet run
```

Mật khẩu tài khoản mới dài 8–128 ký tự, được băm bằng ASP.NET Core Identity PasswordHasher. Không có đăng ký công khai hoặc mật khẩu mặc định. Cookie và kiểm tra principal áp dụng theo [tài liệu ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-8.0).

## Kiểm tra

Profile và đổi mật khẩu: kiểm tra HTTP qua controller/view thật với repository giả đã xác nhận phiên hiện tại vẫn đăng nhập sau khi đổi mật khẩu, hiện thông báo thành công, giữ hạn cookie ghi nhớ, từ chối cookie cũ của phiên khác, từ chối mật khẩu cũ và chấp nhận mật khẩu mới. Profile của Admin ẩn kho, nhân viên kho hiển thị kho; nhập sai mật khẩu hiện tại hoặc xác nhận không khớp vẫn giữ phiên và hiển thị lỗi. Build project thành công không cảnh báo/lỗi.

Quy tắc tạo tài khoản mới: 29 kiểm tra service đạt với role ID tùy ý, gồm loại ADMIN khỏi danh sách, từ chối request tạo ADMIN, xóa kho cho mọi role khác và kiểm tra kho của WAREHOUSE_STAFF. 6 kiểm tra JavaScript đạt cho trạng thái khóa/bắt buộc của ô kho và xóa lựa chọn khi chuyển role. Build project vào thư mục tạm thành công không cảnh báo/lỗi khi ứng dụng hiện tại vẫn chạy.

`dotnet build --no-restore` thành công, không cảnh báo/lỗi. 17 kiểm tra service với repository giả đã đạt: mật khẩu đúng/sai, placeholder, tài khoản khóa/ngừng hoạt động, hash tài khoản mới, trùng username/email, role và kho. Kiểm tra HTTP bao gồm render login, chuyển hướng khách chưa đăng nhập, antiforgery, trường bắt buộc và trang 403. Đã xác minh login từ chối tài khoản `ceo` dùng placeholder trên DB thật. Chưa kiểm tra đăng nhập thành công/tạo tài khoản end-to-end trên DB thật vì chưa có mật khẩu Admin hiện tại.
