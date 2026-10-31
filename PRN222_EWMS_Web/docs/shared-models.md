# Models dùng chung — DB49

- `Models/Entities`: entity của 49 bảng và kiểu dữ liệu của 9 view được scaffold từ DB49.
- `Models/EwmsDbContext.cs`: DbContext dùng chung, đăng ký trong `Program.cs`.
- Cấu hình EF Core cho bảng có trigger nằm trong chính `EwmsDbContext.cs`.
- `Models/ErrorViewModel.cs` phục vụ trang lỗi MVC, không được xóa khi scaffold.

Bộ scaffold ban đầu nằm trong `ScaffoldPreview` được hợp nhất vào các thư mục trên;
không giữ hai bộ entity hoặc hai DbContext song song. Service Sales được điều chỉnh
theo tên thuộc tính và kiểu `DateOnly` của bộ model mới.

## Khi scaffold lần sau

Các tùy chọn thư mục/namespace phù hợp với dự án:

```text
--output-dir Models/Entities --context-dir Models --context EwmsDbContext
--namespace PRN222_EWMS_Web.Models --context-namespace PRN222_EWMS_Web.Models
--no-onconfiguring
```

Đối chiếu thay đổi trước khi ghi đè. Scaffold có thể suy ra quan hệ một-một từ
unique index có điều kiện, mặc dù lịch sử nghiệp vụ cho phép nhiều bản ghi.
Các khóa tham chiếu ghép chứa cột NULL cũng cần kiểm tra lại; không đổi cột
nullable thành bắt buộc chỉ để loại bỏ cảnh báo EF. Ràng buộc database vẫn giữ nguyên.

Không dùng model snapshot để tự tạo migration thay đổi DB49 khi chưa được nhóm
thống nhất. Kiểm tra build, metadata EF và truy vấn sau mỗi lần scaffold.

## Kết nối

Đợt hợp nhất không sửa `appsettings.json` hoặc chạy SQL. Ứng dụng hiện đọc khóa
`ConnectionStrings:MyCnn`; khóa trong cấu hình cần khớp trước khi chạy với DB thật.
Nếu dùng SQL Authentication, không kết hợp tài khoản SQL với `Trusted_Connection=True`.

## Kiểm tra đợt hợp nhất

Kiểm tra độc lập đạt 26 kiểm tra metadata và dịch truy vấn: đủ 49 bảng/9 view;
các cột scope SO/Transfer vẫn nullable; tám quan hệ có unique index theo trạng
thái dùng collection cho lịch sử; tắt direct OUTPUT ở bảng có trigger. Service
Sales sinh được SQL cho dashboard, bộ lọc (gồm ngày DateOnly), chi tiết và lượng
đã xuất DONE, danh sách/chi tiết/form Customer. Truy vấn User kèm Role/Warehouse
vẫn dịch được. 12 câu SQL được chặn bằng interceptor; không mở kết nối database.
Chưa xác minh dữ liệu hoặc thực thi trên SQL Server thật.
Sau khi bỏ `ScaffoldPreview`, build cuối thành công với 0 cảnh báo/0 lỗi;
`git diff --check` sạch. `ErrorViewModel.cs` và cấu hình kết nối được giữ nguyên.
