# 2026-10-29 — Sales Staff

- Triển khai theo phạm vi được duyệt: dashboard, danh sách/chi tiết SO, danh sách/
  chi tiết/tạo/sửa Customer; sidebar Sales dùng chung giao diện Admin.
- Chốt xem toàn bộ SO, thêm lọc đơn của tôi. Quyền sửa đơn chỉ dành người tạo,
  trước bất kỳ Outbound đã lưu; phần sửa đơn thuộc thành viên khác.
- Đối chiếu DB49: snapshot khách và RequestedDate đã tồn tại, không cần tự thêm
  schema. Customer giữ nguyên bốn trường, không quy định điện thoại duy nhất.
- Mapping tách partial DbContext; truy vấn không tracking, danh sách phân trang;
  Customer write lưu actor qua SESSION_CONTEXT cho trigger audit và dọn context.
- Không chạy SQL, không đổi appsettings/connection, không seed hoặc chuyển dữ
  liệu cũ. Product/Outbound/item là mapping cột dùng để đọc, cần mở rộng khi nhóm
  triển khai ghi. Các route tạo SO, Return Order và Notice là điểm tích hợp nhóm.
- Chưa có plan/phase markdown hiện hữu cần sync-back. Build thành công không
  cảnh báo/lỗi; 36 kiểm tra pipeline MVC/Razor và 9 kiểm tra service với dữ liệu
  giả đạt. Sửa binding POST Edit dùng FromRoute sau khi test phát hiện form ID
  có thể ghi đè route ID. Chưa kiểm thử DB49 thật/trình duyệt. Không commit/push.
