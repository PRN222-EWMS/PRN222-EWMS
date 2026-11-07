using System.Text.Json;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.ViewModels.Admin;

public static class AuditLogDisplay
{
    private static readonly TimeZoneInfo VietnamTime = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    public static DateTime ToVietnam(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VietnamTime);
    public static DateTime ToUtcStart(DateOnly date) => TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), VietnamTime);

    public static string Group(string table) => table switch
    {
        "Inbound" or "InboundItem" => "Nhập hàng",
        "PurchaseOrder" or "PurchaseOrderLine" => "Đơn mua hàng",
        "Outbound" or "OutboundItem" or "SalesOrder" or "SalesOrderLine" => "Bán hàng",
        "SerialItem" or "SerialMovement" or "SupplySource" => "Tồn kho",
        "User" or "UserRole" or "Role" => "Người dùng",
        "Warehouse" or "StorageLocation" or "Zone" => "Kho hàng",
        _ => table
    };

    public static string Action(string action) => action switch
    {
        "INSERT" => "Tạo mới",
        "UPDATE" => "Cập nhật",
        "DELETE" => "Xóa",
        _ => action
    };

    public static string Document(AuditLog log)
    {
        var data = Parse(log.AfterData ?? log.BeforeData);
        foreach (var key in new[] { "receiptCode", "pocode", "poCode", "orderCode", "code", "serialNumber" })
            if (data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) && value != "null") return value;
        return $"{log.TableName} · #{log.EntityKey}";
    }

    public static List<AuditFieldChange> Changes(AuditLog log)
    {
        var before = Parse(log.BeforeData);
        var after = Parse(log.AfterData);
        return before.Keys.Union(after.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(key => new AuditFieldChange(key,
                before.GetValueOrDefault(key) ?? "Chưa có", after.GetValueOrDefault(key) ?? "Chưa có"))
            .Where(change => change.Before != change.After).ToList();
    }

    public static string Pretty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "Không có dữ liệu.";
        try { using var doc = JsonDocument.Parse(json); return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true }); }
        catch (JsonException) { return json; }
    }

    private static Dictionary<string, string> Parse(string? json)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return values;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0) root = root[0];
            if (root.ValueKind != JsonValueKind.Object) return values;
            foreach (var property in root.EnumerateObject())
                values[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? "null" : property.Value.GetRawText();
        }
        catch (JsonException) { /* Older audit rows may contain plain text. */ }
        return values;
    }
}

public record AuditFieldChange(string Name, string Before, string After);
