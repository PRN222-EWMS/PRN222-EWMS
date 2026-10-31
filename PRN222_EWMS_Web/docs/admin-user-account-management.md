# Admin quản lý tài khoản nội bộ

## Mục đích

Tài liệu này mô tả yêu cầu triển khai ba chức năng dành cho Admin: xem danh sách tài khoản, xem chi tiết và chỉnh sửa tài khoản. Phần danh sách có tìm kiếm, lọc và phân trang. Hãy dùng nghiệp vụ EWMS, các quyết định đã được người dùng xác nhận và cấu trúc hiện tại của dự án làm căn cứ triển khai.

## Căn cứ và thứ tự ưu tiên

1. Các quyết định được chốt trong cuộc trao đổi này được ưu tiên.
2. Ảnh yêu cầu xác định ba chức năng trong phạm vi: **View account list**, **View account details**, **Edit account**. Actor của cả ba là Admin.
3. Tài liệu `EWMS_Nghiep_vu_va_Ngu_canh_AI.docx` cung cấp nghiệp vụ và ràng buộc tài khoản EWMS. Đặc biệt, việc đổi role, trạng thái hoặc kho không được để công việc đang xử lý bị bỏ dở.
4. Code hiện có là căn cứ về kiến trúc, schema, tên field và quy ước triển khai; không giả định một tính năng đã hoạt động chỉ vì entity hoặc tài liệu có nhắc đến.
5. Tham chiếu giao diện là `docs/DESIGN-notion.md`, phối hợp với các component và layout đang dùng trong ứng dụng.

## Phạm vi

### Bao gồm

- Trang `/User` cho Admin tìm kiếm, lọc, phân trang và mở tài khoản.
- Trang chi tiết một tài khoản nội bộ.
- Trang chỉnh sửa một tài khoản nội bộ, với xác thực quyền ở cả GET lẫn POST.
- Giữ luồng tạo tài khoản hiện có tương thích với quy tắc phân kho cho Warehouse Staff.

### Không bao gồm

- Luồng Manager xem hoặc điều chuyển staff. Đây là nghiệp vụ riêng trong tài liệu nguồn, không thuộc ba chức năng Admin trong ảnh.
- Tạo tài khoản, reset mật khẩu, đăng nhập, tự phục vụ hồ sơ hoặc thay đổi cấu trúc role/permission.
- Thêm bảng/cột, đổi schema, chạy migration, hoặc xây dựng module kho khác.
- Cho phép Admin tạo/gán thêm role `ADMIN`.

Không hiển thị hoặc gửi `PasswordHash` đến trình duyệt. Đặt lại mật khẩu tiếp tục là nghiệp vụ riêng theo UC-010, không nằm trong Edit.

## Quy tắc nghiệp vụ đã chốt

### Phân quyền và role

- Chỉ người đã đăng nhập với role `ADMIN` được truy cập danh sách, chi tiết và Edit. Kiểm tra ở server; ẩn nút trên giao diện không thay thế authorization.
- Danh sách và chi tiết phải cho Admin thấy mọi tài khoản hiện có, kể cả tài khoản có role `ADMIN` nếu dữ liệu đã có.
- Trong Edit, không được gán role `ADMIN` cho user hiện không phải Admin. Nếu user đang có role `ADMIN`, cho giữ role hiện tại hoặc đổi sang role khác; không cho gán `ADMIN` cho tài khoản khác. Server phải thực thi quy tắc này, không chỉ dựa vào danh sách option của form.
- Một tài khoản chỉ có một role.

### Trạng thái

Các giá trị được hỗ trợ là `ACTIVE`, `LOCKED`, `INACTIVE`. Giữ nguyên giá trị lưu trong database; giao diện trình bày bằng nhãn tiếng Việt:

| Giá trị | Nhãn giao diện |
| --- | --- |
| `ACTIVE` | Đang hoạt động |
| `LOCKED` | Bị khóa |
| `INACTIVE` | Ngừng hoạt động |

Danh sách lọc phải bao gồm cả ba trạng thái. Chi tiết hiển thị trạng thái hiện tại. Edit chỉ chấp nhận các giá trị trên; không lưu chuỗi trạng thái tùy ý.

### Role và kho làm việc

- Role `WAREHOUSE_STAFF` bắt buộc có một kho làm việc đang `ACTIVE`.
- Khi đổi từ role khác sang `WAREHOUSE_STAFF`, Admin phải chọn một kho đang hoạt động trước khi lưu.
- Khi đổi từ `WAREHOUSE_STAFF` sang role khác, lưu `WarehouseId = NULL`.
- Các role khác không được gán kho làm việc qua màn hình này.
- Warehouse selector trong Edit chỉ đưa ra kho đang `ACTIVE`. Chi tiết vẫn hiển thị kho đang lưu trên tài khoản để Admin xem xét.
- Luồng Create đang có phải tiếp tục bắt buộc kho active khi tạo Warehouse Staff và bỏ kho với các role còn lại.

### Công việc đang xử lý

- Nếu workflow hiện hữu xác định rõ tài khoản đang có công việc cần tiếp tục, bàn giao hoặc hoàn tất, không cho thay đổi `Role`, `Status` hoặc `Warehouse`; hiển thị lỗi rõ ràng để Admin biết phải xử lý công việc trước.
- Khi có công việc đang xử lý, vẫn cho phép lưu các thay đổi không làm thay đổi phân công: họ tên, email và số điện thoại.
- Chỉ kiểm tra các công việc đang hoạt động/được phân công cho user; không coi mọi chứng từ lịch sử có `UserId` là công việc đang xử lý.
- Trước khi triển khai kiểm tra này, rà các workflow, entity, trạng thái và quan hệ đang có trong repo cùng tài liệu nghiệp vụ. Dùng đúng trạng thái đang được định nghĩa cho từng workflow; không tự thêm trạng thái hoặc suy diễn trạng thái hoàn tất.
- Chỉ chặn thay đổi phân công khi code hiện hữu xác định rõ công việc active theo trạng thái/quan hệ của workflow đó. Không suy diễn trạng thái từ chứng từ lịch sử hoặc chỉ từ navigation của entity.
- Các module xử lý nghiệp vụ chưa có code thì không được giả lập rằng hệ thống đã phát hiện đầy đủ công việc. Không thêm bảng/schema để lấp chỗ trống. Ghi rõ trong kết quả triển khai các workflow đã kiểm tra được và giới hạn còn lại nếu schema/code hiện hữu không đủ căn cứ.
- Tại thời điểm cập nhật tài liệu này, repo chưa có workflow/service để xác định công việc active được giao cho user. Vì vậy, không tuyên bố đã chặn thay đổi role/status/warehouse theo điều kiện công việc dở dang; đây là giới hạn đã biết cho tới khi các workflow liên quan có logic trạng thái đủ căn cứ.

## Chức năng 1: Danh sách tài khoản

### Tìm kiếm

- Có một ô tìm kiếm tra đồng thời `FullName` và `Email`.
- So khớp chuỗi con, không phân biệt hoa thường. Với email null thì bỏ qua field đó.
- Search và các filter kết hợp theo AND; trong điều kiện search, họ tên và email kết hợp theo OR.
- Submit search mới đưa trang về trang đầu.

### Bộ lọc

- Role: mọi role có thể xuất hiện trên tài khoản hiện có, bao gồm `ADMIN` để Admin tìm tài khoản cũ.
- Status: `ACTIVE`, `LOCKED`, `INACTIVE`.
- Warehouse: mọi kho có thể đang được tham chiếu, bao gồm kho ngừng hoạt động để tìm tài khoản còn gán vào kho đó.
- Giá trị mặc định của mỗi filter là “Tất cả”. Thay đổi filter đưa trang về trang đầu.

### Phân trang và thứ tự

- Mặc định 10 tài khoản mỗi trang.
- Sắp xếp `CreatedAt` giảm dần; dùng `UserId` giảm dần làm khóa phụ để thứ tự ổn định.
- Tìm kiếm, filter và page size phải được thực hiện ở database, không tải toàn bộ user về memory để lọc.
- Hiển thị tổng số kết quả và điều khiển trang; giữ nguyên search/filter khi chuyển trang.
- Page number không hợp lệ được chuẩn hóa tối thiểu thành 1. Nếu trang hiện tại vượt quá trang cuối sau khi lọc hoặc xóa dữ liệu, hiển thị trang cuối hợp lệ hoặc trạng thái rỗng một cách nhất quán.
- Khi quay lại danh sách từ Details/Edit, giữ search/filter/page ban đầu nếu request có context đó.

### Nội dung và thao tác

- Hiển thị tối thiểu họ tên, email, role, kho và trạng thái.
- Mỗi dòng có thao tác mở Details và Edit. Giữ nút tạo tài khoản hiện có.
- Empty state phân biệt “chưa có tài khoản” với “không có kết quả khớp điều kiện” khi phù hợp.
- Không đưa thông tin password/hash vào danh sách.

## Chức năng 2: Chi tiết tài khoản

- Mở bằng mã tài khoản; nếu không tồn tại, trả `NotFound`.
- Hiển thị các thông tin account có ý nghĩa với Admin: họ tên, email, số điện thoại, role, trạng thái, kho làm việc nếu có và thời điểm tạo.
- Với tài khoản không phải `WAREHOUSE_STAFF`, trình bày kho là không áp dụng; không hiển thị giá trị kho sai lệch.
- Với tài khoản `WAREHOUSE_STAFF`, hiển thị kho đã gán; nếu không có kho do dữ liệu cũ, nêu rõ chưa được phân công.
- Không hiển thị `PasswordHash`, cookie, claims hoặc `RowVersion` dạng dữ liệu người dùng.
- Có nút Edit và quay lại danh sách; quay lại danh sách phải giữ query context nếu có.

## Chức năng 3: Chỉnh sửa tài khoản

### Field được sửa

- `FullName`, `Email`, `Phone`, `RoleId`, `Status`, `WarehouseId`.
- Email là tùy chọn, phải hợp lệ và duy nhất nếu có giá trị; giá trị rỗng lưu thành `NULL` theo quy ước hiện có.
- Họ tên bắt buộc. Số điện thoại tùy chọn và dùng validation hiện có.
- Mật khẩu không có trong form và không bị thay đổi khi lưu Edit.
- Dùng ViewModel riêng cho Edit; không bind entity `User` trực tiếp từ POST. Chỉ bind các field cho phép sửa để tránh over-posting.

### Validation và lưu

- Validate ở server kể cả khi giao diện đã validate phía client.
- Khi role là `WAREHOUSE_STAFF`, yêu cầu chọn kho active. Khi role khác, xóa `WarehouseId` phía server bất kể request gửi gì.
- Không cho gán mới role `ADMIN`; tài khoản hiện là Admin được giữ role hiện tại hoặc đổi sang role khác. Từ chối trạng thái không hợp lệ, role không tồn tại, kho không tồn tại/ngừng hoạt động hoặc email trùng.
- Bắt lỗi unique constraint SQL Server cho email để xử lý trường hợp hai request cùng lưu; trả lỗi thân thiện, không lộ SQL.
- Đưa `RowVersion` vào ViewModel dưới dạng giá trị ẩn và dùng nó để phát hiện cập nhật đồng thời. Nếu bản ghi đổi sau khi form được mở, không ghi đè âm thầm; báo dữ liệu đã thay đổi và yêu cầu tải lại.
- Ngay trước lúc lưu, dùng bộ kiểm tra công việc active nếu repo có workflow với trạng thái/quan hệ đủ căn cứ. Nếu một field phân công bị thay đổi và điều kiện chặn áp dụng, từ chối lưu thay đổi phân công. Các field liên hệ được phép cập nhật vẫn có thể lưu nếu không vi phạm validation.
- Sau khi lưu, dùng Post/Redirect/Get, báo thành công bằng notice theo style hiện có và giữ đường quay lại danh sách.
- Khi validation/lưu lỗi, giữ giá trị hợp lệ đã nhập, tải lại danh sách role/kho và không trả lại password/hash.

### Phiên đăng nhập và tác động role/status

- Luồng cookie hiện tại kiểm tra lại trạng thái và role của tài khoản trong các request tiếp theo. Khi Admin đổi role hoặc trạng thái tài khoản đang đăng nhập, ứng dụng có thể từ chối cookie ở request kế tiếp theo logic hiện hữu; không bỏ qua kiểm tra cookie để giữ phiên.
- Không tự đăng xuất hoặc thay cookie của một tài khoản khác từ phiên Admin.

## Style giao diện

Dùng `docs/DESIGN-notion.md` làm nguồn token và tinh thần thiết kế: giao diện sáng, nền trắng/off-white, chữ đen/xám, xanh `#0075de` làm màu hành động, đường viền mảnh `#e6e6e6`, khoảng trắng thoáng và góc bo nhẹ. Giữ trang gọn, dễ quét; bảng tài khoản ưu tiên khả năng đọc hơn trang trí.

Triển khai trong shell và component hiện có: `Views/Shared/_Layout.cshtml`, các class `workspace-*`, `workspace-table`, `workspace-card`, `role-badge`, `workspace-notice` trong `wwwroot/css/workspace.css`. Dùng Bootstrap Icons đã có. Bảo đảm responsive, focus-visible, label gắn với input, lỗi gắn với field và thao tác dùng được bằng bàn phím. Không tạo visual language khác cho trang tài khoản.

## Hướng dẫn tích hợp vào code hiện có

- Ứng dụng là ASP.NET Core MVC .NET 8, EF Core SQL Server.
- Controller hiện tại: `Controllers/Admin/UserController.cs`, đã có `[Authorize(Roles = "ADMIN")]`, Index và Create.
- Service hiện tại: `Services/Admin/IUserService.cs`, `Services/Admin/UserService.cs`.
- Repository hiện tại: `Repositories/User/IUserRepository.cs`, `Repositories/User/UserRepository.cs`.
- Entity là `Models/Entities/User.cs`; role và warehouse được nối qua navigation. EF context có unique index cho Email có giá trị và RowVersion concurrency.
- View hiện tại ở `Views/User/Index.cshtml` và `Views/User/Create.cshtml`. ViewModel tạo tài khoản nằm trong `ViewModels/Admin/`.
- Mở rộng các tầng và DI hiện có, giữ controller mỏng; query/filter/page thuộc repository/service thay vì để Razor xử lý.
- Danh sách nên dùng query model và DTO/ViewModel phân trang thay vì trả toàn bộ entity. Giữ `AsNoTracking` cho truy vấn đọc.
- Mọi POST phải có antiforgery token. Bảo vệ Details/Edit bằng authorization ở controller/action.
- Không sửa file EF scaffolded nếu không cần thiết; không thay đổi schema hoặc chạy migration cho ba chức năng này.
- Giữ route MVC tương thích với `/User`, `/User/Details/{id}` và `/User/Edit/{id}` (hoặc route tương đương rõ ràng theo quy ước hiện có).

## Tiêu chí nghiệm thu

1. Người không phải Admin không thể truy cập list/details/edit bằng URL hoặc POST trực tiếp.
2. Admin tìm được tài khoản theo họ tên hoặc email bằng chuỗi con; kết quả không phân biệt hoa thường.
3. Role, trạng thái và kho lọc độc lập và kết hợp đồng thời; trạng thái/kho không hoạt động vẫn tìm được tài khoản cũ.
4. Danh sách phân trang 10 dòng, mới tạo trước, giữ query khi chuyển trang và khi quay lại từ details/edit.
5. Details hiển thị role, status và warehouse phù hợp; không lộ password/hash.
6. Edit cập nhật được các field đã chốt, email được kiểm tra duy nhất, không cho gán mới role ADMIN ở server và mật khẩu không đổi.
7. Warehouse Staff luôn cần kho ACTIVE; đổi sang role khác luôn xóa warehouse assignment.
8. Chặn đổi role/status/warehouse khi có căn cứ từ workflow hiện hữu cho thấy user còn công việc đang xử lý; các field liên hệ không gây đổi phân công vẫn sửa được. Nếu repo chưa có workflow đủ căn cứ, phải nêu rõ giới hạn thay vì suy diễn trạng thái.
9. Lỗi validation, trùng dữ liệu và concurrency được phản hồi rõ; không ghi đè dữ liệu đồng thời âm thầm.
10. Giao diện nhất quán với `DESIGN-notion.md` và các component EWMS hiện có, hoạt động trên màn hình nhỏ và có thể thao tác bằng bàn phím.

