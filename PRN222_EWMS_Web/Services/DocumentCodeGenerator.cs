using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Services;

public enum DocumentType
{
    PurchaseOrder, Inbound, SalesOrder, Outbound, CustomerReturnOrder, ReturnReceipt,
    TransferOrder, TransferDispatch, TransferReceipt, Stocktake, StocktakeInvestigation, InventoryAdjustment
}

/// <summary>Daily document numbers. The caller keeps the transaction open through the document insert.</summary>
public sealed class DocumentCodeGenerator(EwmsDbContext db, TimeProvider clock)
{
    public const string RequestAuditTable = "DocumentCreateRequest";

    private static (string Table, string Column, string Prefix) Definition(DocumentType type) => type switch
    {
        DocumentType.PurchaseOrder => ("PurchaseOrder", "POCode", "PO"),
        DocumentType.Inbound => ("Inbound", "ReceiptCode", "IB"),
        DocumentType.SalesOrder => ("SalesOrder", "SOCode", "SO"),
        DocumentType.Outbound => ("Outbound", "IssueCode", "OUT"),
        DocumentType.CustomerReturnOrder => ("CustomerReturnOrder", "ReturnCode", "RO"),
        DocumentType.ReturnReceipt => ("ReturnReceipt", "ReceiptCode", "RR"),
        DocumentType.TransferOrder => ("TransferOrder", "TransferCode", "TR"),
        DocumentType.TransferDispatch => ("TransferDispatch", "DispatchCode", "TD"),
        DocumentType.TransferReceipt => ("TransferReceipt", "ReceiptCode", "TRR"),
        DocumentType.Stocktake => ("Stocktake", "StocktakeCode", "ST"),
        DocumentType.StocktakeInvestigation => ("Stocktake", "StocktakeCode", "ST-INV"),
        DocumentType.InventoryAdjustment => ("InventoryAdjustment", "AdjustmentCode", "IA"),
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public async Task<string> NextAsync(DocumentType type)
    {
        RequireTransaction();
        var definition = Definition(type);
        var prefix = DayPrefix(definition.Prefix);
        await LockAsync($"EWMS:DocumentCode:{definition.Table}:{prefix}");
        return await ReadNextAsync(definition.Table, definition.Column, prefix);
    }

    // A form preview is not a reservation; save always calls NextAsync again inside its transaction.
    public Task<string> PreviewAsync(DocumentType type)
    {
        var definition = Definition(type);
        return ReadNextAsync(definition.Table, definition.Column, DayPrefix(definition.Prefix));
    }

    private string DayPrefix(string prefix) =>
        $"{prefix}-{clock.GetUtcNow().UtcDateTime.AddHours(7).ToString("yyMMdd", CultureInfo.InvariantCulture)}-";

    private async Task<string> ReadNextAsync(string table, string column, string prefix)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await db.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            // Identifiers come only from Definition; user values never become SQL identifiers.
            command.CommandText = $"""
                SELECT COALESCE(MAX(TRY_CONVERT(bigint, SUBSTRING(c.Code, @Start, 40))), 0)
                FROM (
                    SELECT [{column}] AS Code FROM dbo.[{table}]
                    UNION ALL
                    SELECT COALESCE(JSON_VALUE(AfterData, @CodePath), JSON_VALUE(BeforeData, @CodePath))
                    FROM dbo.AuditLog WHERE TableName = @TableName
                ) AS c WHERE c.Code LIKE @Prefix;
                """;
            var start = command.CreateParameter(); start.ParameterName = "@Start"; start.Value = prefix.Length + 1;
            var pattern = command.CreateParameter(); pattern.ParameterName = "@Prefix"; pattern.Value = prefix + "%";
            command.Parameters.Add(start); command.Parameters.Add(pattern);
            var tableName = command.CreateParameter(); tableName.ParameterName = "@TableName"; tableName.Value = table;
            var codePath = command.CreateParameter(); codePath.ParameterName = "@CodePath"; codePath.Value = "$." + column;
            command.Parameters.Add(tableName); command.Parameters.Add(codePath);
            var maximum = Math.Max(0, Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
            return prefix + checked(maximum + 1).ToString("D3", CultureInfo.InvariantCulture);
        }
        finally { if (opened) await db.Database.CloseConnectionAsync(); }
    }

    /// <summary>Keep the request token separate from the readable number, including reuse of an open draft.</summary>
    public async Task<T> WithRequestAsync<T>(DocumentType type, string token, int actorId,
        Func<long?, Task<T>> action, Func<T, long> documentId)
    {
        RequireTransaction();
        if (actorId <= 0 || !Guid.TryParseExact(token, "N", out _))
            throw new InvalidOperationException("Yêu cầu tạo chứng từ không hợp lệ. Vui lòng tải lại trang.");
        token = token.ToLowerInvariant();
        var table = Definition(type).Table;
        await LockAsync($"EWMS:DocumentRequest:{table}:{token}");
        var keyPrefix = table + ":";
        var previous = await db.AuditLogs.AsNoTracking().Where(a => a.TableName == RequestAuditTable
                && a.RequestId == token && a.ActorUserId == actorId && a.EntityKey.StartsWith(keyPrefix))
            .OrderBy(a => a.AuditId).Select(a => a.EntityKey).FirstOrDefaultAsync();
        long? previousId = null;
        if (previous != null)
        {
            if (!long.TryParse(previous[keyPrefix.Length..], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
                throw new InvalidOperationException("Lịch sử yêu cầu tạo không hợp lệ. Vui lòng tải lại trang.");
            previousId = id;
        }
        var result = await action(previousId);
        var savedId = documentId(result);
        if (previousId == null && savedId > 0)
        {
            db.AuditLogs.Add(new AuditLog
            {
                TableName = RequestAuditTable, EntityKey = keyPrefix + savedId.ToString(CultureInfo.InvariantCulture),
                ActorUserId = actorId, DatabasePrincipal = "EWMS", ActionType = "INSERT", RequestId = token,
                OccurredAt = clock.GetUtcNow().UtcDateTime,
                AfterData = JsonSerializer.Serialize(new { Document = table, DocumentId = savedId }),
                Reason = "Ghi nhận yêu cầu tạo hoặc mở lại phiếu để tránh tạo trùng."
            });
            await db.SaveChangesAsync();
        }
        return result;
    }

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Cấp mã chứng từ cần nằm trong giao dịch lưu chứng từ.");
    }

    private Task LockAsync(string resource) => db.Database.ExecuteSqlInterpolatedAsync($"""
        DECLARE @result int;
        EXEC @result = sys.sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive',
             @LockOwner = 'Transaction', @LockTimeout = 15000;
        IF @result < 0 THROW 51320, N'Chưa cấp được mã chứng từ. Vui lòng thử lại.', 1;
        """);
}
