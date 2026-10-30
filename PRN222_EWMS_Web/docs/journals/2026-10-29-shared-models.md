# 2026-10-29 — Hợp nhất Models DB49

- Chuyển 49 entity bảng và 9 kiểu view từ scaffold tạm sang `Models/Entities`,
  namespace `PRN222_EWMS_Web.Models`; dùng một `EwmsDbContext` trong `Models`.
- Giữ `ErrorViewModel.cs`; thay các entity rút gọn bằng bộ đầy đủ. Service Sales
  dùng tên scaffold và DateOnly, giữ nguyên contract viewmodel của giao diện.
- Hiệu chỉnh tám quan hệ lịch sử bị suy ra một-một từ filtered unique index;
  giữ nullable cho các scope SO/Transfer. Các FK ghép nullable không biểu diễn
  được bằng alternate key EF không được ép thành required; database giữ nguyên.
- Partial context giữ tương thích OUTPUT với trigger. Không sửa cấu hình kết
  nối, không chạy SQL, không tạo migration, không commit/push.
- Review đạt 26 kiểm tra, sinh/chặn 12 câu SQL bằng interceptor với 0 lần mở
  database; gồm model49/9, quan hệ, nullable, truy vấn Sales và User/Role/Warehouse.
  Chưa kiểm tra thực thi trên SQL Server thật.
