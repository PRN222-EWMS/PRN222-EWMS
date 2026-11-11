# Bộ test kiểm kê mở rộng

Chạy **stocktake-test-data-extended.sql** trong SSMS trên database phát triển. Đây là dữ liệu bổ sung; không chạy lại file seed cũ. Mặc định mã bộ test là `QA261110`; nếu đổi `@Tag` trong SQL, thay tiền tố tương ứng trong hướng dẫn này.

Script tạo **18 serial, 4 zone, 7 kế hoạch PLANNED**, PO/Inbound và nguồn hàng riêng. Chưa tạo kết quả đếm, yêu cầu đếm lại hoặc quyết định CEO. Không sửa dữ liệu test cũ, tài khoản hay mật khẩu. Không tự thực thi SQL.

## Tài khoản và vị trí

- A: `phamttrang1025@gmail.com` — đếm dãy A, hoặc toàn Zone 3 trong đợt có hàng chờ xếp.
- B: `trangmooncat@gmail.com` — đếm dãy B, đếm lại dãy A, hoặc hàng chờ xếp.
- Manager: `trangpham13061002@gmail.com`.
- CEO: dùng tài khoản CEO ACTIVE bạn đã tạo. Kết quả cuối script liệt kê email; không tạo mật khẩu.
- `P1`, `P2`: hai sản phẩm ACTIVE được script chọn theo ID; xem tên thật trong kết quả cuối SQL. Không mặc định chúng luôn là Samsung/Dell.
- `G1-2` nghĩa là `QA261110 G1-2`: ô GOOD số 1 trong Zone 2. `G2` và `GB` cũng GOOD; `D1` là ô DAMAGED. Các ô đều có đường dẫn zone/dãy trên UI.

**Thứ tự:** `-01` → `-02` → `-NO-WAIT` → `-03` → `-04`. `-OVERLAP` chỉ thử bắt đầu lúc `-02` đang khóa. `-WHOLE` để cuối hoặc chỉ thử khóa chồng lấn. Các đợt chưa bắt đầu vẫn để PLANNED, không khóa kho.

Mở hai tài khoản bằng **hai profile trình duyệt riêng**, hoặc **một cửa sổ thường và một cửa sổ ẩn danh**. Hai cửa sổ ẩn danh cùng profile vẫn dùng chung phiên. Không đăng xuất A rồi nhầm rằng tab cũ vẫn là A.

## 1. Đếm khớp, nhiều nhân viên và hoàn tất: ST-QA261110-01

1. A bắt đầu; scan `QA261110-M01` GOOD tại G1-1.
2. A gửi kết quả. B chưa gửi thì vòng phải còn mở, chưa kết luận phần B là thiếu; Manager chưa được hoàn tất.
3. B bắt đầu nhiệm vụ cùng đợt; scan `QA261110-M02` GOOD tại GB-1; gửi.
4. Manager xem: **2 khớp, 0 chênh lệch**; không có sản phẩm đủ điều kiện yêu cầu đếm lại.
5. Manager hoàn tất. Đợt COMPLETED, không còn khóa khu vực; không tạo InventoryAdjustment và không đổi vị trí/condition/tồn.

## 2. Các chênh lệch, kiểm lại, CEO từ chối/duyệt: ST-QA261110-02

### Vòng đầu

A đếm dãy A. Với `D04`, ô DAMAGED trong ô GOOD là **bằng chứng tình trạng thực tế**, không phải đã tự chuyển hàng vào nơi phù hợp.

| Serial (tiền tố QA261110-) | Người | Ghi nhận vòng 1 | Mong đợi sau đóng vòng |
|---|---|---|---|
| D01 | A | GOOD / G1-2 | Khớp |
| D02 | A | Bỏ qua | Thiếu |
| D03 | A | GOOD / G2-2 | Khác vị trí |
| D04 | A | DAMAGED / G1-2 | Khác tình trạng |
| D05 | A | DAMAGED / D1-2 | Khác tình trạng và vị trí |
| D06 (P2) | A | GOOD / G1-2 | Khớp; giữ kết quả khi chỉ đếm lại P1 |
| D07 (P2) | B | GOOD / GB-2 | Khớp; ngoài dãy A |
| D08 (quarantine) | A | GOOD / G1-2 | Khớp; vẫn phải đếm; không tự bỏ quarantine |
| D09 (hỏng + quarantine) | A | DAMAGED / D1-2 | Khớp; hỏng vẫn nằm trong tồn vật lý |
| D10 | B | GOOD / GB-2 | Khớp; ngoài dãy A |
| EXTRA01 (**chưa có SerialItem**) | A | Nhập `QA261110-EXTRA01`, chọn P1, GOOD / G1-2 | Thừa; chưa tăng tồn chính thức |

Sau cả A và B gửi: **6 khớp, 1 thiếu, 1 thừa, 1 khác vị trí, 1 khác tình trạng, 1 kết hợp**. Có 11 dòng kết quả; serial chính thức của đợt vẫn 10, EXTRA01 chưa được tạo.

Trong lúc A đếm, thử thêm:

- Scan D01 hai lần: không tăng số lượng, vẫn một dòng/serial/vòng.
- Sau A đã lưu D01, B nhập D01 ở GB-2: chặn vì người khác đã ghi nhận cùng vòng; không ghi đè A.
- A chỉ được chọn vị trí trong dãy A; không chọn GB-2 hoặc zone khác.
- A sửa tình trạng/ghi chú của chính D01 trước gửi: cho phép; cuối cùng trả về GOOD/G1-2.
- Nhập serial rỗng, mã quá 100 ký tự, ghi chú quá 1.000: không lưu. Không có nút Kiểm tra; tra cứu tự chạy, Enter tra ngay.
- Unknown serial: phải chọn sản phẩm, không tự tạo SerialItem.

### Khóa chồng phạm vi

Khi `-02` đã COUNTING/REVIEWING, A thử bắt đầu `ST-QA261110-OVERLAP` hoặc `-WHOLE`: phải bị chặn vì trùng phạm vi. Không sửa trạng thái bằng SQL. Để hai đợt đó PLANNED.

### Gửi CEO nhưng chưa áp dụng; từ chối và xử lý lại

1. Manager chọn **chỉ D04** lập yêu cầu điều chỉnh; nhập lý do.
2. CEO xem lịch sử từng vòng, thử **Duyệt và áp dụng**. Mong đợi **bị chặn**: DAMAGED không hợp vị trí GOOD G1-2. Yêu cầu vẫn SUBMITTED, serial vẫn GOOD/G1-2, không movement mới; không có trạng thái APPLIED nửa chừng.
3. CEO thử Từ chối không nhập lý do: chặn. Nhập lý do “Cần xác minh và bố trí ô DAMAGED hợp lệ” rồi từ chối.
4. Manager phải thấy REJECTED + người quyết định + lý do; tồn chính thức vẫn nguyên; có thể đếm lại/gửi đề nghị mới.

### Đếm lại toàn bộ P1 dãy A bằng người khác

1. Manager chọn P1 → **dãy A của Zone 2** → B → nhập lý do.
2. Chọn người A không được; danh sách phải loại A. Chọn cả Zone 2 có thể không còn ai hợp lệ, vì A và B đều đã đếm P1 ở các nhánh trong zone. Chọn đúng dãy A để B đủ điều kiện.
3. Không chọn P2: P2 đều khớp. Tạo thêm yêu cầu chồng phạm vi khi yêu cầu này chưa hoàn tất phải bị chặn/lựa chọn không còn.
4. B bắt đầu. Nhập D06 (P2) phải bị chặn vì sai model. D10 ở dãy B không thuộc phần dãy A; không cần đếm trong yêu cầu này.
5. B ghi lại **D01, D02, D03, D04, D05, D08, D09, EXTRA01**:
   - D01, D02, D08: GOOD/G1-2. D02 nay được tìm thấy: từ thiếu thành khớp.
   - D03: GOOD/G2-2, xác nhận vẫn khác vị trí.
   - D04, D05: DAMAGED/D1-2, xác nhận đặt trong ô hỏng phù hợp. Chỉ ghi vị trí thực tế nếu thực tế test giả lập đã đưa hàng sang D1-2.
   - D09: DAMAGED/D1-2; giữ quarantine.
   - EXTRA01: GOOD/G1-2, xác nhận còn thừa. Nếu UI yêu cầu chọn sản phẩm thì chọn P1.
6. B gửi. Manager thấy các serial trên có **vòng 2**, lịch sử vòng 1 vẫn giữ. D06/D07/D10 giữ vòng 1; không mất kết quả P2 hay dãy B.
7. Kết quả cuối: **7 khớp, 0 thiếu, 1 thừa, 1 khác vị trí, 0 chỉ khác tình trạng, 2 kết hợp**.

### CEO áp dụng thành công và chống lặp

1. Manager chọn D03, D04, D05, EXTRA01 gửi CEO. Chưa duyệt thì vị trí/condition/serial chính thức chưa đổi.
2. CEO duyệt và áp dụng: header APPLIED; D03 sang G2-2; D04/D05 sang D1-2/DAMAGED; EXTRA01 mới được tạo với OriginType=STOCKTAKE_ADJUSTMENT, source ADJUSTMENT. D04/D05 giữ nguồn gốc mua ban đầu.
3. D08/D09 không bị tự xóa quarantine. Không sửa nguồn/reservation không liên quan khi chỉ đổi vị trí.
4. Mở cùng yêu cầu ở hai tab CEO trước khi duyệt: tab đầu thành công; tab còn lại không được áp dụng lần nữa. Làm mới không tăng serial, không thêm movement lần hai.
5. Manager hoàn tất sau mọi chênh lệch đã giải quyết. Bảng chênh lệch có thể vẫn giữ lịch sử khác vị trí; xem yêu cầu APPLIED/ReviewStatus để biết đã xử lý, không coi lịch sử là lỗi chưa áp dụng.

## 3. Không bao gồm hàng chờ xếp: ST-QA261110-NO-WAIT

Làm đợt này **trước -03**.

1. A bắt đầu, ALL_SCOPE ở đây chỉ là **toàn Zone 3 của kế hoạch**, không phải toàn kho.
2. Danh sách ban đầu chỉ có `QA261110-W01`, không có W02/W03/W04.
3. A ghi W01 GOOD/G1-3 rồi gửi. Manager thấy 1 khớp và hoàn tất.
4. W02/W03/W04 vẫn IN_WAREHOUSE, CurrentLocationID=NULL; không bị coi thiếu bởi đợt NO-WAIT.

## 4. Có hàng chờ xếp, phân công riêng, recount thiếu và CEO giảm tồn: ST-QA261110-03

**Phạm vi hàng chờ xếp là toàn kho**. Nếu có hàng chờ xếp cũ, chúng cũng xuất hiện trong vòng đếm. SQL cuối seed có result set liệt kê riêng; người B phải ghi nhận chúng đúng tình trạng thực tế hoặc chỉ dừng ở xem danh sách, không gửi/duyệt chênh lệch của hàng cũ.

1. A bắt đầu và ghi W01 GOOD/G1-3. A không được chọn “Hàng chờ xếp” vì A chỉ được giao Zone 3.
2. B bắt đầu phần hàng chờ xếp. Vị trí chỉ là **Hàng chờ xếp**, không chọn G1-3.
3. B ghi W02 GOOD/Chờ xếp; W04 DAMAGED/Chờ xếp; bỏ W03. Đếm đúng cả hàng chờ xếp cũ nếu có.
4. Sau A/B gửi: riêng 4 serial W có 3 khớp + W03 thiếu. Quét W02 không biến nó thành hàng đã lên kệ; Location vẫn NULL.
5. Manager yêu cầu đếm lại P1 → Hàng chờ xếp → giao A (người khác B). Vòng đếm lại gồm W02/W03 **và P1 chờ xếp cũ nếu có**, không gồm P2 W04 hay W01 có vị trí.
6. A ghi W02, tiếp tục không thấy W03; gửi kết quả. Manager có căn cứ thiếu ở vòng 2. Không gõ serial W03 chỉ để đánh dấu “thiếu”; thiếu được xác định khi đóng vòng.
7. Manager chọn **chỉ W03** gửi CEO. CEO duyệt.
8. W03 phải LOST; CurrentWarehouseID/CurrentLocationID/CurrentSupplySourceID/CurrentSupplyState=NULL; QuantityDelta=-1; movement IN_WAREHOUSE→LOST. W02/W04 vẫn chờ xếp. Tồn của nhóm W giảm 4→3.
9. Manager hoàn tất. Hàng chờ xếp đã được mở khóa.

**Khác vị trí giữa kệ và chờ xếp:** có thể thử trong khi chưa gửi kết quả: B quét W01 ở Chờ xếp trước khi A ghi nó → hiện cảnh báo khác vị trí và cho ghi bằng chứng trong phạm vi B. Sau đó không thể dùng A ghi đè. Đây là **nhánh thay thế** của ca W, không làm cùng đường chuẩn trên nếu muốn giữ số đếm mong đợi. Cần bộ @Tag mới hoặc Manager xử lý kết quả bằng recount; không reset SQL.

## 5. Sức chứa, rollback và khôi phục bằng đếm lại: ST-QA261110-04

1. A ghi N01 GOOD/**TINY-4**, B ghi N02 GOOD/GB-4; gửi.
2. Manager gửi chênh lệch vị trí N01 cho CEO; CEO duyệt phải bị chặn do vị trí không đủ kích thước/tải trọng.
3. Kiểm tra header còn SUBMITTED, N01 vẫn G1-4, không có movement STOCKTAKE_ADJUSTMENT; không ghi dở dang.
4. CEO từ chối có lý do; Manager yêu cầu recount P1 tại dãy A Zone 4 giao B.
5. B ghi N01 GOOD/G1-4. Kết quả khớp; Manager hoàn tất không cần chỉnh tồn.

## 6. Toàn kho: ST-QA261110-WHOLE (làm cuối)

- Khi còn bất kỳ đợt COUNTING/RECOUNTING/REVIEWING/PENDING_APPROVAL trùng kho: Start phải chặn.
- Sau khi mọi đợt khóa khác đã hoàn tất, A có thể Start. Snapshot phải gồm **toàn bộ serial IN_WAREHOUSE trong kho**, có vị trí hoặc chưa có, kể cả DAMAGED/quarantine. W03 đã LOST phải không xuất hiện.
- Snapshot cũng gồm hàng cũ ngoài bộ test. Muốn kết thúc thật, phải đếm hết hàng thực tế trong kho. Không được gửi rồi duyệt hàng cũ là thiếu chỉ để kết thúc test.
- Đợt này chỉ giao A ALL_SCOPE. B không được bắt đầu/scan bằng ID của A. Nếu sau khi Start không muốn test hết kho thì đừng Start; để PLANNED không khóa. Không cung cấp SQL tự mở khóa/reset vì sẽ làm mất tính đúng của luồng.
- Do chỉ có A được giao cả kho, P1/P2 có thể do A đếm toàn bộ; B có thể là người đếm lại nếu còn đủ điều kiện. Không yêu cầu phải tạo recount khi không có chênh lệch.

## 7. Các ca cần thao tác giao diện, không thể chứng minh chỉ bằng seed

| Ca | Cách thử / mong đợi |
|---|---|
| Tạo/sửa kế hoạch | Dùng màn Manager: chọn kho/ngày tương lai/phạm vi/phân công; lưu nháp được; hoàn tất thiếu lịch hoặc phân công chồng/bỏ sót phải chặn. Không sửa logic teammate trong bộ seed này. |
| Staff không đúng kho/role | Dùng tài khoản đã có của role khác hoặc staff kho khác, mở URL nhiệm vụ: phải bị chặn. Không đổi tài khoản bằng SQL. |
| Gửi xong scan lại | Giữ tab cũ trước gửi, gửi tab khác rồi quay lại lưu: server phải chặn. |
| Model/phạm vi recount | Chỉ P1 khi yêu cầu P1; chỉ phạm vi được giao; người đếm trước không được nhận lại; không tạo request chồng chưa hoàn tất. |
| Hai người thao tác đồng thời | Hai tab/profile Start/Submit/Approve cùng lúc: không nhân đôi snapshot/vòng/serial/movement; lỗi phiên bản cũ phải tải lại. |
| Dữ liệu biến động sau snapshot | Qua nghiệp vụ được nhóm cho phép tạo movement vào serial liên quan; duyệt dựa bằng chứng cũ phải bị chặn. Không INSERT movement giả vào bảng để “test” trên dữ liệu dùng chung. |
| Giảm nguồn đang giữ cho SO | Tạo SO thật qua màn đã triển khai, dùng nguồn mới của bộ test; test thiếu/đổi tình trạng: giảm nguồn cần nhất quán reservation/HasShortage. Không tự INSERT reservation mà bỏ workflow. |
| Picking/allocation đang mở | Qua Outbound/Transfer thật khi nhóm triển khai: Start bị chặn nếu đã có picking trong phạm vi; nếu allocation xung đột xuất hiện sau đó, CEO không được làm mất nhất quán nguồn. Nếu module chưa có thì ghi **chưa test được**, không tính pass. |
| Camera/ảnh | Cần camera/quyền/ảnh mã thật, kiểm nhận mã rồi tra cứu; nhập tay luôn là đường dự phòng. Seed không chứng minh camera hoạt động. |
| Kho inactive/hết thuê | Tạo kho/đợt test qua nghiệp vụ Manager và tài khoản staff tương ứng, không làm inactive kho đang dùng chỉ để thử. Bộ này dùng kho hiện tại nên **không tự tạo ca này**. |

Các nhánh thay thế thay đổi kết quả về sau (chuyển vị trí khác hướng dẫn, bỏ thêm serial, duyệt sớm) nên dùng một @Tag mới cho lần chạy độc lập. Không khẳng định đã test “mọi trường hợp” chỉ vì SQL chạy thành công: đánh dấu từng ca thực tế đã chạy.

## 8. SQL chỉ đọc để đối chiếu

Đổi @Tag nếu dùng bộ khác. Các câu dưới đây không cập nhật DB.

```sql
USE WMSDB;
DECLARE @Tag varchar(16)='QA261110';
-- Nhóm W: trước duyệt thiếu là 4 IN_WAREHOUSE; sau REMOVE W03 là 3.
SELECT COUNT(*) AS TongSerial,
 SUM(CASE WHEN StockState='IN_WAREHOUSE' THEN 1 ELSE 0 END) AS ConTrongKho,
 SUM(CASE WHEN StockState='LOST' THEN 1 ELSE 0 END) AS DaGhiNhanMat
FROM dbo.SerialItem WHERE SerialNumber LIKE @Tag+'-W%';

SELECT s.SerialNumber,s.StockState,s.ItemCondition,s.IsQuarantined,s.CurrentSupplyState,
 w.WarehouseName,l.LocationName,s.CurrentSupplySourceID,s.OriginType,s.OriginalInboundID,s.OriginAdjustmentID
FROM dbo.SerialItem s LEFT JOIN dbo.Warehouse w ON w.WarehouseID=s.CurrentWarehouseID
LEFT JOIN dbo.StorageLocation l ON l.LocationID=s.CurrentLocationID
WHERE s.SerialNumber LIKE @Tag+'-%' ORDER BY s.SerialNumber;

SELECT a.AdjustmentCode,a.Status,a.DecidedAt,a.AppliedAt,l.ScannedSerial,l.AdjustmentType,l.QuantityDelta,l.IsActive
FROM dbo.InventoryAdjustment a JOIN dbo.InventoryAdjustmentLine l ON l.AdjustmentID=a.AdjustmentID
JOIN dbo.Stocktake st ON st.StocktakeID=a.StocktakeID
WHERE st.StocktakeCode LIKE 'ST-'+@Tag+'-%' ORDER BY a.CreatedAt,l.ScannedSerial;

SELECT s.SerialNumber,m.EventType,m.FromState,m.ToState,oldLoc.LocationName AS FromLocation,
 newLoc.LocationName AS ToLocation,m.FromCondition,m.ToCondition,m.AdjustmentID,m.OccurredAt
FROM dbo.SerialMovement m JOIN dbo.SerialItem s ON s.SerialItemID=m.SerialItemID
LEFT JOIN dbo.StorageLocation oldLoc ON oldLoc.LocationID=m.FromLocationID
LEFT JOIN dbo.StorageLocation newLoc ON newLoc.LocationID=m.ToLocationID
WHERE s.SerialNumber LIKE @Tag+'-%' AND m.EventType='STOCKTAKE_ADJUSTMENT'
ORDER BY m.MovementID;

-- So lieu nguon phai khop toan bo serial hien thuoc nguon (nguon duoc chia se giua cac zone test).
SELECT src.SupplySourceID,src.ItemCondition,src.RemainingQuantity,src.EligibleQuantity,
 (SELECT COUNT(*) FROM dbo.SerialItem s WHERE s.CurrentSupplySourceID=src.SupplySourceID AND s.StockState='IN_WAREHOUSE') AS PhysicalCount
FROM dbo.SupplySource src
WHERE EXISTS(SELECT 1 FROM dbo.SerialItem s WHERE s.OriginalInboundID=(SELECT InboundID FROM dbo.Inbound WHERE ReceiptCode='IN-'+@Tag)
 AND s.CurrentSupplySourceID=src.SupplySourceID)
 OR src.InboundID=(SELECT InboundID FROM dbo.Inbound WHERE ReceiptCode='IN-'+@Tag)
ORDER BY src.SupplySourceID;
```

**Trạng thái kiểm chứng bộ tài liệu:** mới đối chiếu tĩnh với schema/seed hiện có; chưa chạy file SQL lên database của bạn. Lưu toàn bộ result set cuối seed để biết ID, model và tên vị trí thật. Nếu SQL báo lỗi, gửi nguyên tab Messages; transaction sẽ rollback, không tắt trigger để chạy tiếp.
