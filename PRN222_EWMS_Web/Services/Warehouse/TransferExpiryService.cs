using System.Data;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Services.Warehouse;

public class TransferExpiryService(EwmsDbContext db, TimeProvider clock, ILogger<TransferExpiryService> logger)
{
    public async Task<bool> CancelIfExpiredAsync(long id, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var order = await TransferDataAccess.LockOrderAsync(db, id, ct);
        bool changed = order != null && await CancelLockedAsync(order, ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
        return changed;
    }

    public async Task<bool> CancelLockedAsync(TransferOrder order, CancellationToken ct = default)
    {
        if (!TransferPolicy.IsExpired(order.Status, order.ExpectedDate, TransferPolicy.Today(clock),
            await TransferDataAccess.HasDocumentsAsync(db, order.TransferOrderId, ct))) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        await TransferDataAccess.ReleaseHoldsAsync(db, order.TransferOrderId, now, ct);
        foreach (var line in await db.TransferOrderLines.Where(l => l.TransferOrderId == order.TransferOrderId).ToListAsync(ct))
            line.CancelledQuantity = line.RequestedQuantity;
        order.Status = "CANCELLED";
        order.ClosedAt = now;
        order.CloseReason = TransferPolicy.ExpiryReason;
        var recipients = await db.Users.Where(u => u.Status == "ACTIVE" && u.Role.RoleName == "MANAGER")
            .Select(u => u.UserId).ToListAsync(ct);
        foreach (var userId in recipients) db.Notices.Add(new Notice
        {
            UserId = userId, Title = "Lệnh chuyển kho tự hủy do quá ETA", NoticeType = "WARNING", CreatedAt = now,
            Content = $"{order.TransferCode}: quá ngày đến {order.ExpectedDate:dd/MM/yyyy}, chưa có phiếu gửi/nhận. Giữ chỗ đã được giải phóng. Hãy tạo lệnh mới nếu vẫn cần chuyển.",
            RelatedUrl = $"/Transfer/Edit/{order.TransferOrderId}"
        });
        await TransferDataAccess.SetActorAsync(db, null, TransferPolicy.ExpiryReason, ct);
        try { await db.SaveChangesAsync(ct); }
        finally { await TransferDataAccess.TryClearActorAsync(db, logger); }
        return true;
    }

    public async Task<int> CancelExpiredAsync(CancellationToken ct = default)
    {
        var today = TransferPolicy.Today(clock);
        var ids = await db.TransferOrders.AsNoTracking().Where(o => (o.Status == "DRAFT" || o.Status == "RELEASED")
            && o.ExpectedDate < today && !o.TransferDispatches.Any() && !o.TransferReceipts.Any())
            .OrderBy(o => o.TransferOrderId).Select(o => o.TransferOrderId).ToListAsync(ct);
        int count = 0;
        foreach (var id in ids) if (await CancelIfExpiredAsync(id, ct)) count++;
        return count;
    }

}

public class TransferExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<TransferExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let startup finish before touching SQL; each pass owns its scoped DbContext.
        await Task.Yield();
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<TransferExpiryService>().CancelExpiredAsync(stoppingToken);
                if (count > 0) logger.LogInformation("Automatically cancelled {Count} expired transfer orders.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Cannot cancel expired transfer orders; will retry next minute."); }
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
