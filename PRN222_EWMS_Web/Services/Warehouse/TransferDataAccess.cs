using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;

namespace PRN222_EWMS_Web.Services.Warehouse;

/// <summary>Shared transfer data operations; the calling service owns the transaction.</summary>
internal static class TransferDataAccess
{
    public static Task<TransferOrder?> LockOrderAsync(EwmsDbContext db, long id, CancellationToken ct = default) =>
        db.TransferOrders.FromSqlInterpolated($"SELECT * FROM dbo.TransferOrder WITH (UPDLOCK,HOLDLOCK) WHERE TransferOrderID={id}")
            .SingleOrDefaultAsync(ct);

    public static async Task<bool> HasDocumentsAsync(EwmsDbContext db, long id, CancellationToken ct = default) =>
        await db.TransferDispatches.AnyAsync(d => d.TransferOrderId == id, ct)
        || await db.TransferReceipts.AnyAsync(r => r.TransferOrderId == id, ct);

    public static async Task ReleaseHoldsAsync(EwmsDbContext db, long id, DateTime now, CancellationToken ct = default)
    {
        var reservations = await db.Reservations.Where(r => r.TransferOrderId == id && r.Status == "ACTIVE").ToListAsync(ct);
        foreach (var r in reservations)
        {
            r.ReleasedQuantity += r.ReservedQuantity - r.ConsumedQuantity - r.ReleasedQuantity;
            r.Status = "CLOSED";
            r.ClosedAt = now;
        }
    }

    public static void ReleaseQuantity(Reservation reservation, int quantity, DateTime now)
    {
        int balance = reservation.ReservedQuantity - reservation.ConsumedQuantity - reservation.ReleasedQuantity;
        if (quantity < 0 || quantity > balance) throw new ArgumentOutOfRangeException(nameof(quantity));
        reservation.ReleasedQuantity += quantity;
        if (reservation.ConsumedQuantity + reservation.ReleasedQuantity == reservation.ReservedQuantity)
        {
            reservation.Status = "CLOSED";
            reservation.ClosedAt = now;
        }
    }

    public static async Task<string?> RemoveUnusedHoldsAsync(EwmsDbContext db, long orderId)
    {
        var holds = await db.Reservations.Where(r => r.TransferOrderId == orderId).ToListAsync();
        if (holds.Count == 0) return null;
        var ids = holds.Select(r => r.ReservationId).ToArray();
        if (holds.Any(r => r.ConsumedQuantity != 0)
            || await db.ReservationConsumptions.AnyAsync(c => ids.Contains(c.ReservationId)))
            return "Giữ chỗ đã có dữ liệu tiêu thụ; không thể thay nguồn của lệnh này.";
        if (await db.Reservations.AnyAsync(r => r.ReplacesReservationId != null
            && ids.Contains(r.ReplacesReservationId.Value) && !ids.Contains(r.ReservationId)))
            return "Giữ chỗ đang được bản ghi khác tham chiếu; không thể thay nguồn của lệnh này.";

        // Existing audit triggers keep the before-image. Delete only unused holds, children first,
        // so the existing context FK is free before the order's source context changes.
        var pending = holds.ToList();
        while (pending.Count > 0)
        {
            var referenced = pending.Where(r => r.ReplacesReservationId != null)
                .Select(r => r.ReplacesReservationId!.Value).ToHashSet();
            var leaves = pending.Where(r => !referenced.Contains(r.ReservationId)).ToList();
            if (leaves.Count == 0) return "Chuỗi thay thế giữ chỗ không hợp lệ; không thể cập nhật lệnh.";
            db.Reservations.RemoveRange(leaves);
            await db.SaveChangesAsync();
            foreach (var leaf in leaves) pending.Remove(leaf);
        }
        return null;
    }

    public static Task SetActorAsync(EwmsDbContext db, int? actor, string? reason, CancellationToken ct = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actor}; EXEC sys.sp_set_session_context @key=N'AuditReason', @value={reason};", ct);

    public static async Task TryClearActorAsync(EwmsDbContext db, ILogger logger)
    {
        try { await SetActorAsync(db, null, null); }
        catch (Exception ex) when (ex is System.Data.Common.DbException or InvalidOperationException)
        {
            // A deadlock may already have aborted the SQL transaction. Preserve the original error.
            logger.LogDebug(ex, "Cannot clear audit context on an aborted connection; transaction/connection will be disposed.");
        }
    }
}
