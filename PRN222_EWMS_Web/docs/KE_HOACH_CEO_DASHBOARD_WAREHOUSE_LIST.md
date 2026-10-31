# Phân tích và triển khai các màn hình CEO và Manager

Ngày lập: 30/10/2026. Cập nhật: 31/10/2026. Trạng thái: **Đã triển khai mã chức năng; người dùng tự kiểm thử. Không truy cập hoặc thay đổi database thật trong đợt triển khai này.**

**Phạm vi cuối theo yêu cầu mới nhất:** CEO có trang chào và danh sách kho; CEO không được xem Details, tạo hoặc sửa kho. Manager dùng chung danh sách, xem Details và tạo/sửa thông tin kho cùng cấu trúc Zone → Dãy → Kệ/Tầng hoặc Ô sàn, kích thước/tải trọng, tình trạng hàng và quy tắc Category–Brand. Mọi chỉnh sửa chỉ được lưu khi phạm vi bị tác động chưa chứa hàng và không bị kiểm kê khóa. Mục 1–10 giữ căn cứ phân tích của phần CEO; phần bổ sung Manager nằm ở mục 11–12, được ưu tiên nếu khác với kế hoạch ban đầu.

Tài liệu này đối chiếu SRS, SDS, tài liệu nghiệp vụ, tám bộ slide PRN222 và mã nguồn hiện tại để chốt cách làm phần CEO. Kết luận: dự án đã có login, tài khoản CEO theo xác nhận của người dùng, sidebar và layout dùng chung. Cần bổ sung nội dung chào CEO và màn danh sách kho trên nền có sẵn.

**Đính chính của người dùng được áp dụng:** trang vào của CEO chỉ cần lời chào, chưa cần số liệu, KPI, biểu đồ hay nội dung giám sát khác. Không xây dựng lại login hoặc tạo thêm tài khoản CEO. File này được khôi phục sau khi người dùng thông báo đã xóa nhầm; trạng thái duyệt được cập nhật theo cuộc trò chuyện.

## 1. Phạm vi đã đề nghị duyệt

| Hạng mục | Kết quả sau khi triển khai |
| --- | --- |
| Sidebar dành cho CEO | Cấu hình phần hiển thị của CEO trong sidebar hiện có; dùng chung layout, CSS, JavaScript, menu tài khoản và cách xác định mục đang chọn. |
| View Dashboard | CEO đăng nhập và vào trang Tổng quan thấy lời chào dành cho CEO. Không gọi truy vấn thống kê. |
| View Warehouse List | Xem danh sách kho và các thông tin cơ bản; nếu chưa có bản ghi thì hiển thị trạng thái rỗng. |
| Kiến thức PRN222 | Tiếp tục dùng MVC, Razor Views, shared layout/partial, DI và truy vấn bất đồng bộ phù hợp với kiến trúc hiện tại. |

Hai dòng trong ảnh là **STT 8 View Dashboard** và **STT 12 View Warehouse List**, đều thuộc CEO, Iteration 1, người phụ trách MinhNH. Đây là số thứ tự bảng phân công, không phải UC-008 và UC-012 trong SRS. Chức năng danh sách kho tương ứng **UC-019 View Warehouses**; Dashboard không có mã riêng trong danh sách 88 UC của SRS.

Không đưa vào đợt giao diện này: tạo/sửa/xóa kho, chi tiết kho, cấu trúc vị trí, sản phẩm, đơn mua/bán, kiểm kê, duyệt điều chỉnh, báo cáo, thông báo, cảnh báo thuê, dashboard có số liệu, SignalR, Worker Service hoặc Blazor. Đầu việc SQL khởi tạo cấu trúc kho được người dùng yêu cầu riêng sau khi duyệt kế hoạch này; nó không mở rộng các màn CEO thành chức năng quản lý kho.

## 2. Nguồn đã đối chiếu và cách sử dụng

### 2.1. Tài liệu nghiệp vụ

| Nguồn | Vị trí dùng làm căn cứ |
| --- | --- |
| [SRS Report 1](<C:/Users/OS/Downloads/SWD392 - Report 1 (Overview & SRS) - SE1972-NET - Group 3 _v1.1 (2).docx>) | Actors 1.3.1; UC List 1.3.2, đặc biệt UC-019; UCs for CEO 1.3.3.2; Screens Flow 1.4.1; Screen Authorization 1.4.2; NFR; BR và CR. |
| [SDS Report 2](<C:/Users/OS/Downloads/SWD392 - Report 2 (SDS) - SE1972-NET - Group3_v1.0 (1).docx>) | Kiến trúc 1.1–1.2; Warehouse 2.2.3 và 3.11.3; hình Create Warehouse 3.9.1.1 để kiểm tra loại kho, trạng thái và vai trò. |
| [Nghiệp vụ và ngữ cảnh EWMS](C:/Users/OS/Downloads/EWMS_Nghiep_vu_va_Ngu_canh_AI.md) | Mục 2–6 về phạm vi, vai trò và kho; mục 14–17 về trạng thái, giao diện, thiết kế và điểm chưa thống nhất; phụ lục UC và Warehouse. |
| [Ảnh phân công](C:/Users/OS/AppData/Local/Temp/codex-clipboard-58035dbd-f643-4467-b425-86252952d5ed.png) | Xác định hai đầu việc CEO và mô tả danh sách kho cần hiển thị. |

Các đoạn hướng dẫn AI, prompt mẫu hoặc yêu cầu cho một lần làm việc khác nằm trong tài liệu được xem là nội dung tham khảo. Chúng không thay thế yêu cầu hiện tại của người dùng. Một tính năng được mô tả trong tài liệu cũng không có nghĩa đã tồn tại trong code.

### 2.2. Phạm vi rà soát

- SRS: tổng quan, danh sách 88 UC, 10 UC chi tiết, phân quyền màn hình, yêu cầu phi chức năng, 15 BR, 16 CR và 66 thông báo; xem trực tiếp hình CEO Use Cases và Screens Flow.
- SDS: kiến trúc, package, mô tả trường của 44 bảng, class specification và mô tả state machine; xem các hình kiến trúc và class/sequence liên quan Create Warehouse.
- Tài liệu nghiệp vụ: các mục nghiệp vụ chính, danh mục UC và schema liên quan; đối chiếu các điểm chưa thống nhất thay vì mặc định mọi nội dung đã được chốt.
- Slide: đọc văn bản và notes của đủ 421 slide trong 8 chương; xem trực tiếp các ảnh code/minh họa trọng tâm về async, DI, MVC, layout và Tag Helpers. Không khẳng định đã chạy các demo hay kiểm từng hình của mọi slide.
- Mã nguồn: rà cấu trúc project, controllers, services, repository, entity, DbContext, shared views, CSS/JavaScript và tài liệu nội bộ. Không chạy ứng dụng, truy vấn database hoặc lấy kết quả kiểm thử cũ làm kết quả của lần rà soát này.

## 3. Dự án hiện đã có gì

Thư mục project: `D:\prn222\PRN222-EWMS\PRN222_EWMS_Web`. Các đường dẫn mã nguồn trong bảng dưới tính từ thư mục này. Mốc mã nguồn khi rà soát: commit `06783dc`; working tree sạch trước khi thêm tài liệu ban đầu.

### 3.1. Nền tảng và chức năng hiện có

| Thành phần | Bằng chứng mã nguồn | Nhận xét |
| --- | --- | --- |
| ASP.NET Core MVC .NET 8 | `PRN222_EWMS_Web.csproj:4`, `Program.cs:14` | Dùng Controllers và Razor Views; chưa phải ứng dụng Razor Pages hoặc Blazor. |
| EF Core SQL Server 8.0.8 | `PRN222_EWMS_Web.csproj:9`, `Program.cs:15` | Đã có DbContext và cấu hình kết nối. |
| Các lớp Controller, Service, Repository | `Controllers/Admin/UserController.cs`, `Services/Admin/UserService.cs`, `Repositories/User/UserRepository.cs` | Tiếp tục cách chia lớp này cho danh sách kho. |
| DI và async/await | `Program.cs:16`, `Repositories/User/UserRepository.cs:9` | Có interface, constructor injection, lifetime scoped và query async. |
| Login, logout, cookie và role | `Controllers/Authentication/AuthenticationController.cs`, `Services/Authentication/` | Login cấp role claim, mặc định chuyển tới Home; logout dùng POST và antiforgery. |
| Bảo vệ trang và kiểm tra tài khoản | `Program.cs:34`, `Services/Authentication/AccountCookieEvents.cs:10` | Trang mặc định cần đăng nhập; cookie được đối chiếu trạng thái tài khoản, role và phiên bản mật khẩu với DB. |
| Danh sách/tạo user | `Controllers/Admin/UserController.cs`, `Views/User/` | Đã có phần quản trị user dành cho ADMIN; không phải toàn bộ chức năng quản trị trong SRS đã hoàn thành. |
| Profile, sửa profile, đổi mật khẩu | `Controllers/Account/AccountController.cs`, `Services/Account/`, `Views/Account/Profile.cshtml` | Tái sử dụng từ menu tài khoản chung. |
| Trang vào chung | `Controllers/HomeController.cs:16`, `Views/Home/Index.cshtml` | Hiện có lời chào theo tên và card dành cho ADMIN; chưa có nội dung riêng thể hiện vai trò CEO. |
| Entity và mapping Warehouse | `Models/Entities/Warehouse.cs`, `Models/EwmsDbContext.cs:18` | Đã có đủ trường cho danh sách cơ bản. |

Trong mã nguồn đã rà soát, DbContext mới có `Role`, `User`, `Warehouse`. Các thiết kế 44 bảng trong SDS/ngữ cảnh chưa được thể hiện đầy đủ thành model và chức năng trong repo. Không dùng số lượng bảng trong tài liệu để kết luận phần mềm đã triển khai tới đâu.

### 3.2. Shared view và sidebar đã có sẵn

| Thành phần | Vai trò cần giữ |
| --- | --- |
| `Views/_ViewStart.cshtml:2` | Đặt layout mặc định `_Layout` cho các view. |
| `Views/Shared/_Layout.cshtml:14` | Render `_Sidebar` một lần; dùng chung topbar, breadcrumb, vùng nội dung và các script. |
| `Views/Shared/_Sidebar.cshtml:6` | Đọc role từ `ClaimTypes.Role`; lọc menu theo role tại dòng 53. |
| `Views/Shared/_Sidebar.cshtml:59` | Xác định active menu bằng controller/action. |
| `wwwroot/css/ewms-sidebar.css` | Shell, sidebar desktop/mobile, thu gọn, màu sắc và kích thước chung. |
| `wwwroot/js/ewms-sidebar.js` | Toggle, lưu trạng thái thu gọn, overlay mobile, Escape, aria-expanded và menu tài khoản. |
| `wwwroot/css/workspace.css` | Heading, card, table, form, button và responsive cho nội dung. |

CEO hiện được khai báo thấy Tổng quan, Thông báo, Kho hàng, Sản phẩm và Duyệt điều chỉnh. Tuy nhiên repo chưa có controller cho `Notice`, `Warehouse`, `Product`, `InventoryAdjustment`. Vì vậy menu hiện hữu chưa đồng nghĩa các màn đó đã hoạt động.

**Cách hiểu yêu cầu sidebar:** bổ sung cách sử dụng cho CEO ngay trong `_Sidebar.cshtml` hiện có. Không tạo `_CeoSidebar`, `_CeoLayout`, shell HTML hoặc script sidebar thứ hai. Hai màn CEO chỉ cung cấp nội dung cho `RenderBody` và đặt `ViewData["Title"]`.

### 3.3. Phong cách giao diện cần tiếp tục

Giao diện thực tế đang được layout nạp là căn cứ trực tiếp. `docs/DESIGN-notion.md` là tài liệu tham khảo thiết kế, không phải lý do để biến hai trang nghiệp vụ thành trang marketing.

| Yếu tố | Hiện trạng cần dùng lại |
| --- | --- |
| Màu | Nền `#f6f5f4`, bề mặt trắng, chữ `#31302e`, chữ phụ `#615d59`, viền `#e6e6e6`, màu hành động `#0075de`. |
| Chữ và khoảng cách | Font stack Inter/Segoe UI/system; H1 40px và 32px ở mobile; card bo 12px, padding 24px/16px. |
| Thành phần | `.workspace-heading`, `.eyebrow`, `.muted`, `.workspace-card`, `.workspace-table`, `.table-wrap`, `.workspace-button`. |
| Responsive | Sidebar chuyển kiểu ở 800px; nội dung có quy tắc ở 640px; bảng dùng wrapper cuộn ngang. |
| Icon | Bootstrap Icons đã được layout nạp; không thêm bộ icon hoặc framework UI mới. |

Chỉ bổ sung CSS nhỏ cho bộ lọc/trạng thái rỗng nếu class hiện có chưa đáp ứng. Không thay màu/font chung, không đưa style riêng vào từng dòng HTML và không sửa toàn bộ hệ thống CSS.

## 4. Yêu cầu nghiệp vụ áp dụng cho hai màn

| Nội dung | Căn cứ | Cách áp dụng lần này |
| --- | --- | --- |
| Landing theo role | SRS CR-04, actor/UC diagram CEO | CEO vào Home thấy nội dung chào CEO theo đính chính mới nhất; chưa cần dashboard số liệu. |
| Danh sách kho và thông tin cơ bản | SRS UC-019 | Có tên, địa chỉ, loại, kỳ thuê và trạng thái hoạt động. |
| CEO và Manager xem danh sách | SRS Screen Authorization 1.4.2 | Endpoint list cho `CEO,MANAGER`, phù hợp cả menu đang có; không phát sinh chức năng quản lý kho cho Manager trong task này. |
| Phân trang | SRS CR-01 | Có phân trang khi đủ dữ liệu; danh sách rỗng không hiện các nút trang vô nghĩa. |
| Tìm/lọc | SRS CR-02, ngữ cảnh 15.1 | Tìm theo tên/ID, lọc loại, trạng thái và khoảng thời gian thuê phù hợp. |
| Định dạng ngày | SRS CR-08 | Hiển thị `dd/MM/yyyy`; ngày thiếu không bị biến thành ngày mặc định. |
| Kiểm tra quyền trên server | SRS CR-12, Security 4.2.4 | Giữ cookie auth; thêm role authorization cho endpoint kho, không chỉ ẩn menu. |
| CEO xem thông tin, không quản lý kho | SRS BR-15 và phạm vi hai đầu việc | Không có nút thêm/sửa/xóa/đổi trạng thái kho. |

CEO xem các kho trong hệ thống, không lọc theo `User.WarehouseId`. Code tạo user hiện chỉ gán kho làm việc cho `WAREHOUSE_STAFF`; CEO có thể có `WarehouseId = null` và vẫn cần xem danh sách kho.

## 5. Thiết kế chức năng

### 5.1. Sidebar CEO dùng chung

Trong lần triển khai này, phần điều hướng chính của CEO có hai mục hoạt động:

| Nhãn | Route | Hành vi |
| --- | --- | --- |
| Tổng quan | `/Home/Index` | Trang chào CEO; active khi đang ở Home/Index. |
| Kho hàng | `/Warehouse/Index` | Danh sách kho; active khi đang ở Warehouse/Index. |

Menu tài khoản cuối sidebar tiếp tục dùng Profile và Đăng xuất hiện tại. Đề nghị **tạm ẩn riêng với CEO** các link Thông báo, Sản phẩm, Duyệt điều chỉnh chưa có màn hình để không dẫn tới 404. Việc này chỉ giới hạn điều hướng ở giai đoạn hiện tại, không thay đổi quyền nghiệp vụ lâu dài trong SRS. Các vai trò khác giữ cách hiển thị hiện có.

Không tạo thêm một mục Dashboard cạnh Tổng quan, không lặp Kho hàng và không chép lại menu tài khoản. Cách thu gọn, mở trên mobile và đánh dấu active tiếp tục do CSS/JS hiện có xử lý.

### 5.2. Trang chào CEO

Giữ route `/Home/Index` để phù hợp luồng login hiện tại. Trong `HomeController.Index`, nếu `User.IsInRole("CEO")` thì trả view `CeoDashboard`; các role khác tiếp tục nhận `Views/Home/Index.cshtml` như trước.

View đề nghị: `Views/Home/CeoDashboard.cshtml`, sử dụng `_ViewStart` và `_Layout` hiện tại. Nội dung tối thiểu:

- Title và breadcrumb: `Tổng quan`.
- Tiêu đề: `Xin chào, CEO`.
- Có thể hiển thị tên từ `User.Identity.Name` bằng Razor encoding để nhận biết tài khoản hiện tại.
- Không có KPI, bảng thống kê, biểu đồ, cảnh báo hoặc card chức năng phụ. Người dùng đi tới danh sách kho bằng sidebar.

Không cần DashboardService, DashboardRepository, DashboardViewModel hoặc truy vấn Warehouse cho lời chào. Không cần tạo controller/area CEO riêng. Không sửa login redirect vì login đã về Home khi không có local ReturnUrl; local ReturnUrl hiện tại vẫn được tôn trọng.

Home vẫn là trang chung cần đăng nhập. Không gắn `[Authorize(Roles = "CEO")]` lên toàn bộ HomeController vì sẽ ảnh hưởng ADMIN và các role khác; nhánh render nội dung CEO được chọn bằng role ở server.

### 5.3. Màn danh sách kho

Route: `/Warehouse/Index`. Tiêu đề: `Danh sách kho`. Controller kiểm tra `[Authorize(Roles = "CEO,MANAGER")]`. Giao diện chỉ đọc, dùng bảng và bộ lọc theo phong cách `Views/User/Index.cshtml`.

| Cột | Trường hiện có | Quy tắc hiển thị |
| --- | --- | --- |
| ID | `WarehouseId` | ID thật để nhận diện; không gọi là WarehouseCode hoặc tạo mã WH001 giả. |
| Tên kho | `WarehouseName` | Văn bản đã encode, cho phép xuống dòng khi dài. |
| Địa chỉ | `Address` | Địa chỉ kho; không tự bổ sung quận/tỉnh hoặc bản đồ. |
| Loại kho | `WarehouseType` | `MAIN` → Kho chính; `RENTED` → Kho thuê. |
| Thời hạn thuê | `RentalStartDate`, `RentalEndDate` | Kho thuê hiển thị từ ngày đến ngày; kho chính hiển thị “Không áp dụng”. |
| Trạng thái | `Status` | `ACTIVE` → Đang hoạt động; `INACTIVE` → Ngừng hoạt động. |

`RowVersion` là trường kỹ thuật, không cần hiển thị hoặc thay đổi cho thao tác đọc. Với dữ liệu thuê thiếu ngày, hiển thị “Chưa có thông tin” tại phần bị thiếu. Với type/status chưa nhận diện, hiển thị giá trị có sẵn hoặc nhãn chưa xác định, không tự gán thành MAIN/ACTIVE.

Đề nghị hành vi tìm kiếm và phân trang để đáp ứng yêu cầu chung của SRS:

- Form GET; người dùng bấm “Tìm kiếm” và có nút “Xóa bộ lọc”. Không cần AJAX.
- Từ khóa tìm theo tên kho; nếu là số nguyên hợp lệ thì cho phép khớp ID. Trim khoảng trắng; không dựng SQL từ chuỗi nhập.
- Bộ lọc loại: Tất cả/Kho chính/Kho thuê. Bộ lọc trạng thái: Tất cả/Đang hoạt động/Ngừng hoạt động. Mặc định xem cả kho ACTIVE và INACTIVE.
- Khoảng ngày thuê là bộ lọc tùy chọn. Đề nghị hiểu là kỳ thuê giao với khoảng ngày người dùng chọn, tính cả hai đầu; khi có lọc ngày thì chỉ xét kho thuê có ngày đáp ứng. Đây là quy ước triển khai đề xuất, SRS không ghi công thức giao khoảng cụ thể.
- Nếu nhập cả hai ngày thì ngày bắt đầu bộ lọc không lớn hơn ngày kết thúc. Giữ các giá trị người dùng đã nhập khi báo lỗi.
- Mặc định 10 dòng/trang, là lựa chọn giao diện đề xuất. Sắp xếp theo tên kho rồi ID để ổn định thứ tự. Đổi bộ lọc đưa về trang 1; chuyển trang giữ bộ lọc; chuẩn hóa số trang không hợp lệ.
- Lọc, đếm và phân trang tại query database. Không tải toàn bộ kho về để phân trang ở trình duyệt.

Trong trạng thái chưa có dữ liệu, vẫn nhìn thấy tiêu đề, bộ lọc và cấu trúc bảng. Không thêm dòng mẫu, nút Details hay nút tạo kho để lấp chỗ trống.

### 5.4. Trạng thái rỗng và lỗi dữ liệu

Người dùng đã xác nhận có màn login và tài khoản CEO; trước đó xác nhận đăng nhập được, chỉ chưa có dữ liệu kho. Kế hoạch dùng phần xác thực hiện tại và đọc bảng Warehouse qua EF Core, không cần tài khoản giả hoặc chế độ đăng nhập xem trước. Khi người dùng chạy SQL khởi tạo kho theo đầu việc bổ sung, danh sách cần hiển thị bản ghi thật đó.

| Trường hợp | Kết quả hiển thị |
| --- | --- |
| Query thành công, bảng Warehouse không có bản ghi | “Chưa có kho nào trong hệ thống.” Bảng không có dòng dữ liệu; không cần phân trang. |
| Có dữ liệu nhưng không khớp bộ lọc | “Không tìm thấy kho phù hợp.” Cho phép xóa bộ lọc. |
| Bảng/schema thiếu hoặc truy vấn kho thất bại | Thông báo không tải được dữ liệu kho và ghi log kỹ thuật; không biến thành danh sách rỗng để che lỗi. |
| Phiên hết hạn hoặc tài khoản không còn hợp lệ | Giữ hành vi login/403 hiện có của phần xác thực. |

Chưa kiểm tra DB runtime trong lần lập kế hoạch. Nếu schema Warehouse thực tế chưa khớp model, phải báo đúng tình trạng khi triển khai; không tự chạy migration, tạo bảng hoặc sửa chuỗi kết nối. Lỗi xảy ra trong middleware xác thực cũng không thể được xử lý bằng một empty list trong WarehouseController.

## 6. Cách triển khai theo kiến trúc hiện tại

```text
Sidebar dùng chung
  → WarehouseController.Index có kiểm tra role
  → IWarehouseService kiểm tra bộ lọc và tạo ViewModel
  → IWarehouseRepository truy vấn read-only và phân trang
  → EwmsDbContext.Warehouses
  → Views/Warehouse/Index.cshtml trong _Layout hiện có
```

Repository sử dụng query LINQ được EF tham số hóa, `AsNoTracking`, thứ tự ổn định và các phương thức async để đếm/lấy trang dữ liệu. Service định hình dữ liệu cho ViewModel; view không truy vấn database. Controller nhận service qua constructor; service/repository đăng ký scoped như các lớp đang có.

Không dùng `GetActiveWarehousesAsync()` của UserRepository cho màn CEO: phương thức này phục vụ dropdown gán kho và lọc mất các kho INACTIVE. Thêm repository kho riêng, giữ nguyên hành vi của module tài khoản.

Các thao tác trên cùng DbContext được await tuần tự; không dùng `Task.Run`, PLINQ hoặc cố chạy query đồng thời để thể hiện kiến thức parallel. Không thêm package hoặc nâng phiên bản .NET chỉ để khớp slide demo.

### 6.1. Danh sách file dự kiến

Tất cả đường dẫn trong bảng tính từ thư mục project ở mục 3. Đây là danh sách dự kiến, chưa phải file đã sửa/tạo.

| Thao tác | File | Mục đích |
| --- | --- | --- |
| Sửa | `Controllers/HomeController.cs` | Chọn view chào CEO theo role; giữ nhánh Home cho các role khác. |
| Thêm | `Views/Home/CeoDashboard.cshtml` | Lời chào CEO dùng shared layout. |
| Sửa | `Views/Shared/_Sidebar.cshtml` | Dùng menu hiện có cho hai màn CEO, xử lý các link chưa triển khai riêng trong phần hiển thị CEO. |
| Thêm | `Controllers/Warehouse/WarehouseController.cs` | GET Index và kiểm tra quyền CEO/Manager. Folder không tạo Area; controller vẫn có route Warehouse. |
| Thêm | `Views/Warehouse/Index.cshtml` | Bộ lọc, bảng, empty state và phân trang. |
| Thêm | `ViewModels/Warehouse/WarehouseListViewModel.cs` | Dữ liệu danh sách, tổng số bản ghi, số trang và trạng thái hiển thị. |
| Thêm | `ViewModels/Warehouse/WarehouseListItemViewModel.cs` | Các trường của một dòng kho. |
| Thêm | `ViewModels/Warehouse/WarehouseListFilter.cs` | Các đầu vào GET và kiểm tra bộ lọc. |
| Thêm | `Services/Warehouse/IWarehouseService.cs`, `WarehouseService.cs` | Xử lý bộ lọc và ánh xạ dữ liệu cho view. |
| Thêm | `Repositories/Warehouse/IWarehouseRepository.cs`, `WarehouseRepository.cs` | Đọc danh sách/đếm dữ liệu kho thật theo điều kiện. |
| Sửa | `Program.cs` | Đăng ký DI của phần kho; giữ nguyên cấu hình authentication và routing chung. |
| Sửa nếu cần | `wwwroot/css/workspace.css` | Bổ sung tối thiểu style cho bộ lọc/empty state bằng token đang có. |

Không cần thay Warehouse entity, DbContext mapping, `_Layout`, `_ViewStart`, login, cookie events, cấu hình kết nối hoặc JavaScript sidebar để đáp ứng thiết kế trên.

## 7. Áp dụng kiến thức trong tám chương

Các khoảng slide dưới là vị trí nội dung đã đối chiếu, không phải yêu cầu phải chép nguyên demo.

| Chương và số slide | Nội dung liên quan | Áp dụng vào dự án và hai màn |
| --- | --- | --- |
| [01 Networking Programming](<D:/prn222/Chapter 01 - Networking Programming - Copy - Copy.pptx>) — 60 | Client/server 5–10, URL 11–14, request/response 18–22, HttpClient 24–32, TCP/UDP 35–59. | MVC đã có HTTP request/response và routing. Dùng GET cho list/filter. Không cần TCP/UDP server hay HttpClient để đọc DB nội bộ. |
| [02 Async và Parallel](<D:/prn222/Chapter 02 - Asynchronous and Parallel Programming in .NET.pptx>) — 55 | TAP 42–54, async I/O 47–49; lưu ý PLINQ ở slide 40. | Warehouse list dùng async/await xuyên các lớp cho query thật. Trang chào CEO không có I/O nên không thêm async giả. |
| [03 Dependency Injection](<D:/prn222/Chapter 03 - Dependency Injection in .NET.pptx>) — 54 | DIP/IoC 21–28, container 30–36, lifetime 33, constructor injection 38–41. | Tiếp tục interface, constructor injection và scoped service/repository đang có. |
| [04 ASP.NET Core MVC](<D:/prn222/Chapter 04 - Building Web Application using ASP.NET Core MVC.pptx>) — 76 | Routing 14–17; MVC 19–23; Views/Shared 32; ViewModel 21/37/44; Tag Helpers 45; binding/validation 46–51. | Nền tảng chính: route, controller, typed list ViewModel, Razor view, bộ lọc GET và validation. |
| [05 Razor Pages](<D:/prn222/Chapter 05 - Building Websites Using ASP.NET Core Razor Pages.pptx>) — 72 | Phân biệt MVC/Pages 5–7; layout/ViewStart/ViewImports 8–11; partial 9; Tag Helpers 22–25. | Áp dụng cơ chế Razor dùng chung cho MVC. Có `.cshtml` không đồng nghĩa đã dùng PageModel/Razor Pages. |
| [06 Blazor](<D:/prn222/Chapter 06 - Building a Web App with Blazor and ASP .Net Core.pptx>) — 33 | Khái niệm 3–9, component 11–15, demo 17–32. | Chưa có Blazor trong repo. Không thêm cho trang chào/list; nếu môn học bắt buộc thì nhóm chốt phạm vi khác. |
| [07 Real-Time Communication](<D:/prn222/Chapter 07 - Real-Time Communication.pptx>) — 50 | Push/pull 3–5, Hubs 11–16, user/group 17–24, EF + SignalR 41–49. | Chưa có SignalR. Có thể liên hệ cập nhật nhập/xuất hoặc thông báo tương lai; hai màn chưa yêu cầu realtime. |
| [08 Worker Service](<D:/prn222/Chapter 08 - Background Tasks with Worker Service - Copy.pptx>) — 21 | Công việc nền 3–5, hosted/background service 5–6, DI/logging 7–8, demo 15–20. | Chưa có worker. Có thể dùng cho cảnh báo thuê khi được duyệt; AdminBootstrap không phải Worker Service. |

Không tuyên bố cả tám chương đã được triển khai chỉ vì đã đọc slide. MVC, async, DI và shared Razor là phần phù hợp nhất. Các ví dụ dùng Startup, package cũ, query đồng bộ hoặc EF trực tiếp trong controller không thay thế cấu trúc code hiện tại.

## 8. Điểm chưa thống nhất giữa các nguồn

| Điểm cần lưu ý | Cách xử lý trong lần này |
| --- | --- |
| UC-019 nói CEO/Manager xem list và details, nhưng Screen Authorization 1.4.2 chỉ đánh dấu Manager ở Warehouse Details; Screens Flow chỉ nối CEO tới Warehouse. | Làm danh sách đủ thông tin theo ảnh; chưa tạo Details. Không kết luận CEO vĩnh viễn không được xem chi tiết. |
| BR-15 còn WarehouseCode; SDS, ngữ cảnh 6.1/C15 và entity hiện tại đã bỏ. | Giữ tên/ID, không thêm WarehouseCode hoặc migration. |
| Handover 16.3 nói kiến trúc lớp chưa chốt trong SRS, nhưng SDS 1.1–1.2 và repo đã có Controller/Service/Repository. | Bám SDS và mã nguồn hiện tại. |
| Dashboard theo role chưa có bộ KPI cụ thể. | Theo đính chính: chỉ lời chào CEO. |
| MSG-011 nhắc thuê trước một tháng; ngữ cảnh Q14 chưa chốt mốc hết hạn. | Chỉ hiển thị kỳ thuê, chưa thêm cảnh báo hoặc tự đổi một tháng thành 30 ngày. |
| Status hoạt động và thời hạn thuê độc lập. | Giữ ACTIVE/INACTIVE; không thêm EXPIRED hoặc tự khóa kho hết hạn. |

Các khác biệt rộng hơn về actor sản phẩm, chứng từ hoặc chính sách tài khoản nằm ngoài đợt này. EWMS quản lý thiết bị theo serial; số tồn, reservation, incoming và sức chứa có quy tắc riêng. Không suy diễn số liệu vận hành từ Warehouse để thêm vào trang chào.

## 9. Trình tự triển khai

1. Hoàn tất đầu việc SQL khởi tạo kho được người dùng yêu cầu trước phần UI.
2. Kiểm tra working tree để tránh ghi đè công việc nhóm.
3. Bổ sung view chào CEO tại Home và điều chỉnh menu CEO trong sidebar chung.
4. Tạo Warehouse list ViewModels, repository/service/controller và DI.
5. Hoàn thiện bộ lọc, bảng, phân trang, empty state và lỗi dữ liệu.
6. Build, kiểm tra quyền, điều hướng và shared layout trên desktop/mobile.
7. Báo file thay đổi, kết quả kiểm tra thực tế và hạn chế nếu môi trường thiếu điều kiện. Không tự thực thi SQL đã bàn giao hoặc thay đổi database để có dữ liệu demo.

## 10. Tiêu chí nghiệm thu

| Nhóm | Điều kiện đạt |
| --- | --- |
| Đúng phạm vi | Trang CEO chỉ có lời chào, không KPI/biểu đồ/module khác. |
| Shared UI | Một sidebar/layout; đúng CSS, icon, topbar và menu tài khoản hiện tại. |
| Landing | CEO login không ReturnUrl thì tới Home chào CEO; role khác giữ Home hiện tại. |
| Điều hướng | Tổng quan/Kho hàng đúng route/active; không trùng mục hoặc thêm link CEO 404. |
| Phân quyền | Anonymous về login; CEO/Manager được list; ADMIN/staff bị chặn khi mở URL list. Home role khác vẫn hoạt động. |
| Dữ liệu rỗng | Hiện thông báo rỗng khi query thành công và không có kho; không có bản ghi giả. Trang chào không phụ thuộc query kho. |
| Dữ liệu thật | Hiển thị đúng MAIN/RENTED, ACTIVE/INACTIVE, địa chỉ/kỳ thuê; không bỏ kho INACTIVE. |
| Ngày và NULL | dd/MM/yyyy; MAIN không có kỳ thuê giả; nullable không gây lỗi. |
| Filter/paging | Tìm tên/ID, lọc loại/trạng thái/ngày, reset, giữ filter khi đổi trang, kiểm tra đầu vào. |
| Lỗi dữ liệu | Không đổi lỗi query/schema thành không có kho; không phơi bày chi tiết kỹ thuật. |
| Hồi quy | Giữ Login, Profile, Logout, User Admin và sidebar role khác. |
| Trải nghiệm | Bảng dài/cuộn ngang, sidebar desktop/mobile, Escape, focus bàn phím và heading. |
| Kỹ thuật | Build MVC/Razor thành công; không thêm dependency/schema/thao tác ghi cho hai màn chỉ đọc. |

Kiểm tra có dữ liệu có thể dùng fixture trong kiểm thử, không ghi kho mẫu vào DB đang dùng. Kiểm thử tự động tập trung vào quyền và query/filter khi cần; shared UI kiểm tra trực tiếp. Các tiêu chí trên là kế hoạch, chưa phải kết quả đã đạt.

## 11. Trạng thái bàn giao

Đã triển khai Home chào CEO; danh sách kho chung có tìm tên/ID, lọc loại/trạng thái/kỳ thuê, 10 dòng/trang; phân biệt danh sách rỗng, không khớp bộ lọc và lỗi truy vấn. Sidebar hiện có được dùng cho cả CEO/Manager, chỉ hiện Tổng quan/Kho hàng trong đợt này, giữ menu Profile/Logout. Trang Details/Create/Edit tiếp tục đánh dấu Kho hàng đang được chọn.

Login, tài khoản, cookie, chuỗi kết nối, `_Layout`, `_ViewStart` và JavaScript sidebar giữ nguyên. Hai file UserRepository chỉ bổ sung alias `WarehouseEntity` để tránh trùng tên với namespace repository kho mới; không đổi hành vi tài khoản.

Các lớp mới tiếp tục MVC → Service → Repository, constructor injection, scoped lifetime, async I/O, typed ViewModel và Razor shared partial đúng nội dung các chương 2–5. Không thêm dependency vào project ứng dụng, không thêm Blazor/SignalR/Worker.

## 12. Bổ sung Manager theo ảnh phân công ngày 31/10/2026

Người dùng thay yêu cầu Details của CEO bằng bốn chức năng Manager: View Warehouse List, View Warehouse Details, Edit Warehouse và Create Warehouse. Quyền được kiểm tra ở server, không chỉ ẩn nút.

| Route | CEO | Manager | Hành vi |
| --- | --- | --- | --- |
| `/Home/Index` | Trang chào CEO | Home hiện có | Dùng login/redirect hiện tại. |
| `/Warehouse/Index` | Được xem | Được xem | Cùng bộ lọc và bảng; Manager có liên kết ở tên kho và nút Tạo kho. |
| `/Warehouse/Details/{id}` | Bị chặn | Được xem | Thông tin chung, cây vị trí, quy tắc và sức chứa cấu hình. |
| `/Warehouse/Create` GET/POST | Bị chặn | Được dùng | Lưu thông tin kho, chuyển sang cấu hình đầy đủ các vị trí. |
| `/Warehouse/Edit/{id}` GET/POST | Bị chặn | Được dùng | Sửa tên, địa chỉ, loại, trạng thái và kỳ thuê khi toàn kho chưa chứa hàng/không bị kiểm kê khóa. |
| `/Warehouse/Setup/{id}` GET | Bị chặn | Được dùng | Cây cấu trúc, thêm Zone/vị trí con, sửa từng vị trí và hiển thị các phần cấu hình còn thiếu. |
| `/Warehouse/ConfigureLocation/{id}` GET/POST | Bị chặn | Được dùng | Tạo/sửa một vị trí thuộc kho; kiểm tra hàng, khóa kiểm kê, cấu trúc, quy tắc và RowVersion trước khi lưu. |
| `/Warehouse/CompleteSetup/{id}` POST | Bị chặn | Được dùng | Đối chiếu cấu trúc hiện tại rồi trở về Details; không tạo cờ hoàn tất hoặc bảng/cột mới. |

Các role ADMIN/staff không được dùng controller kho. Người chưa đăng nhập đi về login. Các POST có antiforgery; không có endpoint xóa kho.

### 12.1. Details

Nguồn dữ liệu là các bảng hiện hữu `Warehouse`, `StorageLocation`, `ZoneStorageRule`, `Category`, `Brand`, đối chiếu với `EWMS_SQLServer_Schema.sql` và tài liệu nghiệp vụ. Entity/mapping phục vụ đọc và lưu chức năng kho; kiểm tra hàng và khóa kiểm kê sử dụng `SerialItem`, `Stocktake`, `StocktakeScope` hiện hữu. Không chạy migration hay tạo bảng/cột.

- Đọc vị trí theo đúng WarehouseID, dựng cây Zone → Dãy → Kệ → Tầng hoặc Dãy → Ô sàn, không truy vấn từng node.
- Zone hiển thị STANDARD/RESERVE và từng cặp loại hàng/hãng đã lưu; Brand NULL được hiểu là mọi hãng của category đó. Không tự gán Zone A/B/C/D cho sản phẩm nào.
- Tầng kế thừa GOOD/DAMAGED từ kệ. Ô sàn có condition riêng và ghi rõ không xếp chồng.
- Kích thước đọc bằng cm và hiển thị bằng m; thể tích tầng bằng dài × rộng × cao / 1.000.000, diện tích ô sàn bằng dài × rộng / 10.000. Tải trọng theo MaxWeightKg.
- Tổng chỉ cộng các tầng hoặc các ô sàn, không cộng lại ở cấp cha, không cộng m² với m³. Đây là sức chứa cấu hình, gồm cả vị trí INACTIVE; không phải dung lượng còn trống hoặc diện tích mặt bằng toàn kho.
- Các thông số thuyết minh của script như diện tích nhà 1.200 m²/lối đi không có cột lưu trong Warehouse nên không tự đưa vào giao diện như dữ liệu thật.
- Kho mới/chưa cấu hình có trạng thái rỗng. Vị trí sai parent/type hoặc có vòng được tách ra thông báo, không gây đệ quy vô hạn. ID kho không tồn tại trả trang 404; lỗi truy vấn trả 503.

### 12.2. Tạo và sửa kho

Tên và địa chỉ bắt buộc, giới hạn 150/500 ký tự, trim trước khi lưu. Tên kho được kiểm tra trùng và bắt lỗi unique index khi hai yêu cầu trùng đồng thời. Cho phép nhiều kho MAIN; không ép MAIN phải duy nhất.

Loại chỉ nhận MAIN/RENTED; trạng thái chỉ nhận ACTIVE/INACTIVE. Kho thuê bắt buộc có hai ngày và ngày kết thúc không trước ngày bắt đầu. Kho chính không lưu kỳ thuê; đổi từ RENTED sang MAIN xóa hai ngày thuê. Thời hạn thuê không tự đổi trạng thái hoạt động.

Form sửa gửi RowVersion; khi có thay đổi đồng thời, trả 409 và yêu cầu tải lại thông tin, giữ nội dung người dùng đang nhập. Cập nhật thông tin kho thành công chuyển về Details; tạo kho chuyển sang Setup. Mapping các bảng được ghi tắt SQL OUTPUT trực tiếp khi cần tương thích audit trigger.

Theo yêu cầu mở rộng, luồng tạo kho bao gồm cấu hình UC-022–024: lưu kho → thêm Zone → thêm Dãy → thêm Kệ/Tầng hoặc Ô sàn → hoàn tất. Mỗi bước đã lưu được giữ lại để tiếp tục cấu hình; lỗi ở bước sau không xóa các bước trước. Setup chỉ cho hoàn tất khi các nhánh đang hoạt động đã có đủ vị trí con và quy tắc cần thiết. Đây là thao tác điều hướng sau khi kiểm tra cấu trúc, không phải một trạng thái mới trong database.

- Zone chọn STANDARD hoặc RESERVE. STANDARD lưu các cặp Category–Brand; Brand để trống là mọi hãng của category đó. RESERVE không lưu quy tắc giới hạn category/brand. Không tự gán Zone cho một SKU/ProductID cụ thể vì schema nghiệp vụ không có quy tắc này.
- Kiểu để kệ hoặc để sàn được cấu hình ở Dãy theo schema. Dãy có vị trí con không đổi kiểu trực tiếp; không xóa lịch sử hoặc chuyển các node sang cha khác. Có thể ngừng sử dụng dãy cũ khi đủ điều kiện rồi tạo dãy phù hợp.
- Kệ và Ô sàn chọn hàng tốt/hàng hỏng; Tầng kế thừa tình trạng hàng của Kệ. Kích thước và tải trọng được nhập ở Tầng/Ô sàn, theo cm/kg. Tầng có đủ dài/rộng/cao; Ô sàn có dài/rộng và chiều cao giới hạn tùy chọn, không xếp chồng.
- Ngừng hoạt động một vị trí sẽ ngừng cả các vị trí con trong cùng lần lưu. Kích hoạt lại không tự kích hoạt các vị trí con; các vị trí cha phải đang hoạt động.

### 12.3. Chặn sửa khi đang có hàng và bảo vệ dữ liệu đồng thời

Yêu cầu mới nhất của người dùng được ưu tiên: **chỉ sửa khi phạm vi tương ứng chưa chứa sản phẩm**, kể cả đổi tên, trạng thái, tăng kích thước hoặc đổi quy tắc.

- Sửa thông tin kho kiểm tra toàn bộ serial `IN_WAREHOUSE` trong kho, bao gồm hàng chưa được xếp vị trí (`CurrentLocationID` NULL).
- Sửa Zone/Dãy/Kệ kiểm tra chính vị trí đó và toàn bộ vị trí con. Sửa Tầng/Ô sàn kiểm tra chính điểm đặt hàng. Hàng tốt, hàng hỏng, giữ chỗ hoặc đang picking vẫn được tính nếu còn ở kho.
- Nhánh khác có hàng không làm khóa một vị trí rỗng độc lập. Thêm vị trí con cũng phải kiểm tra phạm vi vị trí cha; thêm Zone mới không sửa cấu trúc của các Zone khác.
- Phạm vi kiểm kê đang COUNTING, RECOUNTING, REVIEWING hoặc PENDING_APPROVAL chặn thao tác ảnh hưởng tới nó. Khóa tại một node cũng bảo vệ các node con; sửa node cha không được bỏ qua khóa ở nhánh con.
- Trước khi ghi, hệ thống đọc lại tồn và phạm vi khóa trong cùng transaction Serializable với thao tác lưu. Khóa cập nhật theo WarehouseID tuần tự hóa các lần thay đổi cấu trúc cùng kho; RowVersion phát hiện form sửa đã cũ. Các câu truy vấn nhận tham số, không ghép trực tiếp dữ liệu form vào SQL.
- Giao diện khóa form khi biết đang có hàng/kiểm kê, đồng thời server kiểm tra lại lúc lưu; việc chỉ ẩn hoặc khóa nút không được dùng làm biện pháp bảo vệ duy nhất.

Các module nhận hàng/putaway/transfer phải áp dụng quy tắc trạng thái, thuê, cấu trúc và sức chứa khi được triển khai; đợt này không bổ sung các luồng đó. Không tạo file test hoặc file kiểm tra phụ; không chạy ứng dụng với database thật, seed/reset hoặc migration trong đợt hoàn thiện này. Người dùng tự kiểm thử chức năng.

Cách dùng: đăng nhập bằng tài khoản hiện có → Kho hàng. CEO xem danh sách. Manager bấm tên kho để xem Details, chọn Chỉnh sửa kho hoặc Cấu hình vị trí. Tạo kho sẽ mở tiếp Setup để xây dựng cấu trúc. Không cần chạy lại script seed hoặc reset database để dùng các màn hình này.
