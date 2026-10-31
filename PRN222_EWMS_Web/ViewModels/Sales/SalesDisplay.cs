namespace PRN222_EWMS_Web.ViewModels.Sales;

public static class SalesDisplay
{
    public static string Status(string status) => status switch
    {
        "DRAFT" => "Nháp", "SUBMITTED" => "Chờ xuất", "PARTIALLY_FULFILLED" => "Xuất một phần",
        "FULFILLED" => "Đã xuất đủ", "CLOSED" => "Đã đóng", "CANCELLED" => "Đã hủy",
        "PICKING" => "Đang lấy hàng", "READY" => "Sẵn sàng xuất", "DONE" => "Hoàn tất", _ => status
    };
}
