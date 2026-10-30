# Sales Staff

## Phạm vi

Các màn hình: `/SalesDashboard`, `/SalesOrder`, `/SalesOrder/Details/{id}`,
`/Customer`, `/Customer/Details/{id}`, `/Customer/Create`, `/Customer/Edit/{id}`.
Controller của module yêu cầu role `SALES_STAFF`. Customer dùng chung cho Sales;
SO mặc định hiển thị tất cả, bộ lọc `Mine` chỉ lấy đơn của tài khoản hiện tại.

Chi tiết SO ưu tiên snapshot khách hàng trên chứng từ; trường liên hệ trống trong
snapshot không được thay bằng liên hệ hiện tại. Số đã xuất chỉ đếm item còn hiệu lực
của Outbound `DONE`. Số còn xuất = yêu cầu - đã xuất - đã hủy/đóng.

Customer chỉ có tên (bắt buộc, tối đa 200 ký tự), địa chỉ (500) và điện thoại (30).
Không bắt buộc điện thoại/địa chỉ; không ép tên hay điện thoại duy nhất. Tạo/sửa
Customer không cập nhật snapshot của SO/Outbound đã có. Ghi dữ liệu đặt
`SESSION_CONTEXT('ActorUserID')` bằng user đã xác thực trên cùng connection với
SaveChanges và xóa context trong finally trước khi đóng connection.

## Database và tích hợp nhóm

Module nhắm tới schema **DB49.sql**, gồm RequestedDate và snapshot khách hàng.
Không chạy SQL, không tạo/migrate database và không đổi connection string trong
đợt này. Cần chuẩn bị DB49 và chọn đúng connection trước khi kiểm thử DB thật.
DB49 chỉ seed sáu role, không có tài khoản/đơn/khách mẫu.

Models hiện dùng chung bộ scaffold đầy đủ của DB49 trong `Models/Entities`, gồm
49 bảng và 9 view; DbContext là `Models/EwmsDbContext.cs`. Không giữ bộ entity Sales
rút gọn song song. Cấu hình tắt direct OUTPUT cho bảng có trigger nằm ngay trong
`EwmsDbContext.cs`, không có file DbContext riêng cho Sales. Service Sales dùng tên và navigation của bộ scaffold mới, chuyển
`DateOnly` sang dữ liệu ngày hiển thị. Xem `shared-models.md` về các mapping đã hiệu
chỉnh sau scaffold. Các nghiệp vụ ghi vẫn phải triển khai đầy đủ transaction/rule.

`CanEdit` chỉ thể hiện điều kiện để tích hợp giao diện: người tạo là user hiện tại,
trạng thái DRAFT/SUBMITTED, chưa có bất kỳ Outbound nào được lưu. **Chưa triển khai
Edit Sales Order**. Endpoint chỉnh sửa của thành viên khác bắt buộc kiểm lại các
điều kiện phía server, không tin cờ/nút trên giao diện.

Sidebar có các điểm tích hợp dự kiến `/SalesOrder/Create`,
`/CustomerReturnOrder/Index`, `/CustomerReturnOrder/Create`; thông báo ở header
dùng `/Notice/Index`. Các chức năng này thuộc phần thành viên khác và chưa được
triển khai trong đợt này. Không thêm xử lý submit/reservation/picking/trả hàng.

## Kiểm tra

Build thành công, không cảnh báo/lỗi. Kiểm tra độc lập đạt 45 kiểm tra bằng
harness tạm, không kết nối database: 36 kiểm tra qua pipeline MVC/Razor thật với
service giả (render đủ 7 màn, chặn khách chưa đăng nhập/role Admin, 404, trạng thái
bộ lọc, lỗi khoảng ngày, antiforgery, validation, redirect, HTML encoding và ID
route/form không khớp) và 9 kiểm tra service với repository giả (tất cả/đơn của
tôi, điều kiện CanEdit, chỉ đếm active DONE, snapshot, trim/ID khi tạo và cập nhật).
Kiểm tra ID phát hiện và sửa POST Edit bằng `[FromRoute] int id`, tránh form Id
ghi đè ID trên URL. Harness được dọn sau kiểm tra. Chưa kiểm thử truy vấn/thao tác
trên SQL Server DB49 thật hoặc kiểm tra giao diện bằng trình duyệt.
Không thực hiện commit/push trong đợt này.
