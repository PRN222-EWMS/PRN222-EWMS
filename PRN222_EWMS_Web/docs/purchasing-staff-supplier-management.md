# Purchasing Staff quản lý nhà cung cấp

## Mục tiêu

Đặc tả bốn chức năng dành cho Purchasing Staff: xem danh sách, xem chi tiết, tạo mới và chỉnh sửa nhà cung cấp. Dùng nghiệp vụ trong ảnh yêu cầu, các quyết định đã chốt và schema hiện tại làm căn cứ. Khi quyết định trong cuộc trao đổi khác nội dung ảnh, quyết định mới nhất được ưu tiên.

## Actor và phân quyền

- Actor của cả bốn usecase là `PURCHASING_STAFF`; không phải Admin.
- Bảo vệ controller/action ở server bằng `[Authorize(Roles = "PURCHASING_STAFF")]` cho list, details, Create GET/POST và Edit GET/POST.
- Sidebar chỉ quyết định link nào hiển thị. Không xem việc ẩn link là thay thế authorization.
- Repo hiện chưa có `SupplierController`; tạo các action và views tương ứng khi triển khai module này. Sidebar đã có mục `Supplier/Index`.

## Schema hiện có

Entity `Supplier` có các field:

| Field | Quy tắc schema hiện có | Quy tắc chức năng |
| --- | --- | --- |
| `SupplierId` | Khóa chính | Dùng làm route key; không cho sửa từ form |
| `SupplierName` | Bắt buộc, tối đa 200 ký tự | Bắt buộc; tên nhà cung cấp được phép trùng |
| `Address` | Nullable, tối đa 500 ký tự | Tùy chọn |
| `Phone` | Nullable, tối đa 30 ký tự | Tùy chọn; đúng 10 số bắt đầu bằng số 0, không khoảng trắng, không +84 |
| `Email` | Nullable, tối đa 254 ký tự | Tùy chọn; phải hợp lệ và duy nhất nếu có giá trị |
| `Status` | `varchar(32)`, mặc định `ACTIVE` | Chỉ nhận `ACTIVE` hoặc `INACTIVE` |

Các quyết định dữ liệu:

- Không bổ sung field mã số thuế, người liên hệ hoặc field ngoài entity hiện tại.
- Chuẩn hóa khoảng trắng đầu/cuối trước khi lưu; giá trị trắng ở field tùy chọn được lưu thành `NULL`.
- Email phải duy nhất khi có giá trị và so sánh không phân biệt hoa thường. Cho phép nhiều nhà cung cấp không có email.
- Thêm unique filtered index ở SQL Server cho Email khác `NULL`, cùng kiểm tra trùng ở service. Bắt lỗi unique constraint khi hai request đồng thời, trả thông báo thân thiện và không lộ SQL.
- Trước khi áp dụng index, kiểm tra dữ liệu hiện có sau khi trim và so sánh email không phân biệt hoa thường; nếu có trùng thì dừng migration và báo để xử lý thủ công, không tự xóa/gộp nhà cung cấp vì có thể mất lịch sử đơn mua hàng.
- Entity có trigger `TR_Supplier_Audit`; cập nhật bằng EF/repository hiện tại để trigger tiếp tục ghi nhận thay đổi. Không xóa hoặc thay trigger.

## Vòng đời và liên kết đơn mua hàng

- Tạo mới luôn đặt `Status = ACTIVE`; Create không cho gửi trạng thái khác từ client.
- Edit cho phép chuyển giữa `ACTIVE` và `INACTIVE`; supplier có thể được kích hoạt lại.
- Không có chức năng xóa cứng. Dùng `INACTIVE` để ngừng sử dụng và giữ nguyên SupplierID cùng dữ liệu liên kết.
- Supplier hiện được `PurchaseOrder.SupplierId` tham chiếu. Đổi thông tin hoặc chuyển trạng thái không được xóa, tách hoặc ghi đè liên kết đơn mua hàng lịch sử.
- Supplier `INACTIVE` không được chọn cho đơn mua hàng mới. Khi module Purchase Order được xây dựng, selector/API phải chỉ trả supplier `ACTIVE`; vẫn giữ dữ liệu supplier cũ để xem các đơn lịch sử.

## UC-PS-SUP-01: Xem danh sách và tìm nhà cung cấp

### Tìm kiếm và lọc

- Một ô tìm kiếm so khớp chuỗi con không phân biệt hoa thường theo `SupplierName`, `Email` và `Phone`; email/điện thoại null được bỏ qua.
- Các field tìm kiếm kết hợp OR; tìm kiếm và lọc trạng thái kết hợp AND.
- Bộ lọc trạng thái gồm `ACTIVE`, `INACTIVE` và `Tất cả`. Mặc định khi vào trang là `ACTIVE`.
- Khi người dùng thay đổi search hoặc status filter, chuyển về trang 1.

### Phân trang và hiển thị

- 10 nhà cung cấp mỗi trang; sắp xếp `SupplierName` tăng dần, dùng `SupplierId` tăng dần làm khóa phụ.
- Tìm kiếm, lọc, đếm và phân trang thực hiện tại database trước khi lấy kết quả; không tải toàn bộ Supplier về memory để lọc.
- Hiển thị tối thiểu tên, email, số điện thoại và trạng thái; địa chỉ hiển thị trong Details.
- Có tổng số kết quả, điều khiển trang và giữ search/filter khi chuyển trang hoặc quay lại từ Details/Edit.
- Empty state phân biệt danh sách trống với không có kết quả khớp điều kiện.
- Mỗi dòng có thao tác Details và Edit. Có thao tác tạo nhà cung cấp mới.

## UC-PS-SUP-02: Xem chi tiết nhà cung cấp

- Mở bằng `SupplierId`; nếu không tồn tại, trả `NotFound`.
- Hiển thị tên, địa chỉ, số điện thoại, email và trạng thái.
- Không hiển thị hoặc quản lý các quan hệ `SupplierProduct` trong Details. Đây là quyết định riêng cho phạm vi này dù user story trong ảnh có nhắc đến quan hệ sản phẩm.
- Có thao tác Edit và quay lại danh sách; quay lại danh sách giữ search/filter/page nếu có.

## UC-PS-SUP-03: Tạo nhà cung cấp

- Form gồm tên bắt buộc, địa chỉ, điện thoại và email tùy chọn. Trạng thái khởi tạo luôn `ACTIVE`.
- Dùng ViewModel riêng; không bind entity `Supplier` trực tiếp từ POST.
- Server kiểm tra độ dài, tên bắt buộc, định dạng email/điện thoại và email duy nhất; client validation chỉ hỗ trợ trải nghiệm, không thay thế kiểm tra server.
- Sau khi tạo thành công, dùng Post/Redirect/Get, chuyển tới Details và hiển thị thông báo thành công.
- Khi validation/lưu lỗi, giữ lại giá trị đã nhập, trừ thông tin nhạy cảm nếu có.

## UC-PS-SUP-04: Chỉnh sửa nhà cung cấp

- Cho phép sửa tên, địa chỉ, số điện thoại, email và trạng thái. Không sửa `SupplierId`.
- Kiểm tra email duy nhất loại trừ chính Supplier đang sửa; tên trùng được phép.
- Không xóa hoặc thay đổi liên kết Supplier với đơn mua hàng lịch sử. Không thêm/xóa `SupplierProduct` trong màn Create/Edit này.
- Không có action Delete; muốn ngừng sử dụng thì chuyển sang `INACTIVE`.
- Edit mở được từ cả List và Details. Hủy hoặc lưu thành công đều quay về Details; sau khi lưu hiện thông báo và giữ context list để quay lại.
- Dùng Post/Redirect/Get sau khi lưu; khi lỗi validation, giữ giá trị hợp lệ đã nhập.

## Luồng và kiến trúc tích hợp

- Route theo MVC convention: `Supplier/Index`, `Supplier/Details/{id}`, `Supplier/Create`, `Supplier/Edit/{id}`.
- Tái sử dụng kiến trúc đang có: Controller → Service → Repository → `EwmsDbContext`; ViewModels riêng cho list/details/create/edit.
- Giữ truy vấn list và search ở repository/database; controller mỏng và không đưa entity trực tiếp ra view.
- Đăng ký service/repository mới trong `Program.cs` theo DI hiện có. Không tạo sidebar/layout khác; dùng `_Layout.cshtml`, các class `workspace-*` và Bootstrap Icons đang có.
- EF model hiện scaffolded. Unique email index là thay đổi schema duy nhất được cho phép trong phạm vi này; tạo migration hoặc schema update tương ứng và không thay đổi field nghiệp vụ khác.
- Mọi POST có antiforgery token. Không thêm chức năng reset/xóa tài khoản, quản lý quan hệ SupplierProduct hoặc nghiệp vụ Purchase Order vào scope này.

## Giới hạn repo hiện tại

- Entity và quan hệ đã có, nhưng repo chưa có `SupplierController`, `SupplierService`, `SupplierRepository`, ViewModels hoặc Supplier views.
- Sidebar link `Supplier/Index` hiện không mở được trang cho tới khi module được triển khai.
- Repo không có migration Supplier riêng hiện hữu; kiểm tra quy trình cập nhật schema của dự án trước khi áp dụng unique index vào database.
- Không suy diễn các trạng thái Supplier khác ngoài `ACTIVE`/`INACTIVE` đã chốt.

## Style giao diện

Dùng tinh thần `docs/DESIGN-notion.md` và component hiện hữu: nền sáng, chữ tối, màu xanh hành động, đường viền mảnh, khoảng trắng thoáng, góc bo nhẹ. Dùng `workspace-card`, `workspace-form`, `workspace-table`, `workspace-button`, `workspace-link` và `workspace-notice`; bảo đảm responsive, focus-visible, label/input liên kết và validation message gắn với field.

## Tiêu chí nghiệm thu

1. Chỉ Purchasing Staff truy cập được list/details/create/edit bằng URL hoặc POST trực tiếp.
2. List tìm theo chuỗi con không phân biệt hoa thường trên tên, email, điện thoại; status mặc định Active nhưng có thể chọn Inactive/Tất cả.
3. Search và filter kết hợp đúng; phân trang database 10 dòng, tên A–Z, ổn định theo SupplierId.
4. Details hiển thị field Supplier hiện có; không hiển thị quan hệ SupplierProduct.
5. Create yêu cầu tên, field liên hệ tùy chọn, mặc định Active; tên trùng được phép, email duy nhất nếu có.
6. Edit đổi được field đã chốt và status Active/Inactive; supplier có thể kích hoạt lại.
7. Không xóa cứng Supplier; lịch sử PurchaseOrder vẫn tham chiếu đúng SupplierID sau khi sửa hoặc chuyển Inactive.
8. Supplier Inactive không được chọn cho đơn mua hàng mới khi module PO được triển khai.
9. Email unique index cho phép nhiều giá trị NULL; race khi lưu email trùng được xử lý thân thiện.
10. Mọi lỗi validation giữ lại dữ liệu đã nhập; UI theo style chung và thao tác được bằng bàn phím.
