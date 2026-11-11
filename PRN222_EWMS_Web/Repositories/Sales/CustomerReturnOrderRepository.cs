using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.CustomerReturnOrder;
using WarehouseEntity = PRN222_EWMS_Web.Models.Warehouse;

namespace PRN222_EWMS_Web.Repositories.Sales;

public class CustomerReturnOrderRepository(EwmsDbContext db) : ICustomerReturnOrderRepository
{
    public async Task<List<EligibleSalesOrderOptionDto>> GetEligibleSalesOrdersAsync(CancellationToken cancellationToken = default)
    {
        // 1. Get candidate Sales Orders that have at least one completed Outbound shipment
        var candidateOrders = await db.SalesOrders.AsNoTracking().Include(so => so.Customer)
            .Where(so => so.Outbounds.Any(ob => ob.Status == "DONE"))
            .OrderByDescending(so => so.OrderDate).ThenByDescending(so => so.SalesOrderId).ToListAsync(cancellationToken);

        var result = new List<EligibleSalesOrderOptionDto>();

        foreach (var order in candidateOrders)
        {
            var eligibleLines = await GetEligibleSalesOrderLinesAsync(order.SalesOrderId, null, cancellationToken);
            int eligibleCount = eligibleLines.Count(l => l.EligibleQuantity > 0);

            if (eligibleCount > 0)
            {
                result.Add(new EligibleSalesOrderOptionDto(
                    order.SalesOrderId,
                    order.Socode,
                    order.Customer?.CustomerName ?? "Khách lẻ",
                    order.OrderDate.ToDateTime(TimeOnly.MinValue),
                    order.WarehouseId,
                    eligibleCount
                ));
            }
        }

        return result;
    }

    public Task<List<WarehouseOptionDto>> GetActiveWarehousesAsync(CancellationToken cancellationToken = default) =>
        db.Warehouses.AsNoTracking().Where(w => w.Status == "ACTIVE").OrderBy(w => w.WarehouseName)
            .Select(w => new WarehouseOptionDto(w.WarehouseId, w.WarehouseName, w.Status)).ToListAsync(cancellationToken);

    public Task<WarehouseEntity?> GetWarehouseWithZonesAsync(int warehouseId, CancellationToken cancellationToken = default) =>
        db.Warehouses.AsNoTracking().Include(w => w.StorageLocations).ThenInclude(sl => sl.ZoneStorageRules)
            .FirstOrDefaultAsync(w => w.WarehouseId == warehouseId, cancellationToken);

    public Task<List<Product>> GetProductsByIdsAsync(IEnumerable<int> productIds, CancellationToken cancellationToken = default)
    {
        var idList = productIds.Distinct().ToList();
        return db.Products.AsNoTracking().Where(p => idList.Contains(p.ProductId)).ToListAsync(cancellationToken);
    }

    public Task<SalesOrder?> GetSalesOrderDetailsAsync(long salesOrderId, CancellationToken cancellationToken = default) =>
        db.SalesOrders.AsNoTracking().Include(so => so.Customer).Include(so => so.Warehouse)
            .FirstOrDefaultAsync(so => so.SalesOrderId == salesOrderId, cancellationToken);

    public async Task<List<EligibleSalesOrderLineDto>> GetEligibleSalesOrderLinesAsync(
        long salesOrderId,
        long? currentEditingReturnOrderId = null,
        CancellationToken cancellationToken = default)
    {
        var lines = await db.SalesOrderLines.AsNoTracking().Include(sol => sol.Product)
            .Where(sol => sol.SalesOrderId == salesOrderId).OrderBy(sol => sol.SolineId).ToListAsync(cancellationToken);

        if (lines.Count == 0) return [];

        var solineIds = lines.Select(l => l.SolineId).ToList();

        // Step 1: Shipped quantities per SolineId (Outbound.Status == 'DONE' and IsActive == true)
        var shippedCounts = await db.OutboundItems.AsNoTracking()
            .Where(oi => solineIds.Contains(oi.SolineId) && oi.IsActive && oi.Outbound.Status == "DONE")
            .GroupBy(oi => oi.SolineId).Select(g => new { SolineId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SolineId, x => x.Count, cancellationToken);

        // Step 2: Completed returned quantities per SolineId (ReturnReceipt.Status == 'DONE', IsActive == true, joined via ReturnLine)
        var returnedCounts = await db.ReturnReceiptItems.AsNoTracking()
            .Where(rri => solineIds.Contains(rri.ReturnLine.SolineId) && rri.IsActive && rri.ReturnReceipt.Status == "DONE")
            .GroupBy(rri => rri.ReturnLine.SolineId).Select(g => new { SolineId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SolineId, x => x.Count, cancellationToken);

        // Step 3: Outstanding active commitments for OPEN or PARTIALLY_RECEIVED return orders
        var activeReturnLinesQuery = db.CustomerReturnOrderLines.AsNoTracking()
            .Where(crol => solineIds.Contains(crol.SolineId) && (crol.ReturnOrder.Status == "OPEN" || crol.ReturnOrder.Status == "PARTIALLY_RECEIVED"));

        if (currentEditingReturnOrderId.HasValue)
        {
            activeReturnLinesQuery = activeReturnLinesQuery.Where(crol => crol.ReturnOrderId != currentEditingReturnOrderId.Value);
        }

        var activeReturnLines = await activeReturnLinesQuery
            .Select(crol => new {
                crol.ReturnLineId,
                crol.SolineId,
                crol.RequestedQuantity,
                crol.CancelledQuantity,
                ReceivedCount = db.ReturnReceiptItems.Count(rri => rri.ReturnLineId == crol.ReturnLineId && rri.IsActive && rri.ReturnReceipt.Status == "DONE")
            })
            .ToListAsync(cancellationToken);

        var committedCounts = activeReturnLines.GroupBy(x => x.SolineId).ToDictionary(
            g => g.Key,
            g => g.Sum(x => Math.Max(0, x.RequestedQuantity - x.CancelledQuantity - x.ReceivedCount))
        );

        // Step 4: Assemble Option A calculation for each sales order line
        var result = new List<EligibleSalesOrderLineDto>();
        foreach (var line in lines)
        {
            int shipped = shippedCounts.GetValueOrDefault(line.SolineId, 0);
            if (shipped == 0) continue; // Only lines with completed shipped items are eligible for return

            int returned = returnedCounts.GetValueOrDefault(line.SolineId, 0);
            int committed = committedCounts.GetValueOrDefault(line.SolineId, 0);
            int eligible = Math.Max(0, shipped - returned - committed);

            result.Add(new EligibleSalesOrderLineDto(
                line.SolineId,
                line.ProductId,
                line.Product?.ProductCode ?? string.Empty,
                line.Product?.ProductName ?? string.Empty,
                line.Product?.Unit,
                shipped,
                returned,
                committed,
                eligible
            ));
        }

        return result;
    }

    public Task<CustomerReturnOrder?> FindByIdAsync(long returnOrderId, bool asNoTracking = false, CancellationToken cancellationToken = default)
    {
        var query = db.CustomerReturnOrders
            .Include(ro => ro.SalesOrder).ThenInclude(so => so.Customer)
            .Include(ro => ro.Warehouse).Include(ro => ro.CreatedByUser)
            .Include(ro => ro.CustomerReturnOrderLineReturnOrders).ThenInclude(line => line.Soline).ThenInclude(sol => sol.Product)
            .Where(ro => ro.ReturnOrderId == returnOrderId);

        if (asNoTracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string> GenerateReturnCodeAsync(CancellationToken cancellationToken = default)
    {
        // 1. Require an active database transaction for concurrency-safe sequential code generation
        var currentTx = db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Yêu cầu một giao dịch đang hoạt động để sinh mã đơn trả hàng an toàn.");

        var year = DateTime.UtcNow.Year;
        var prefix = $"RO-{year}-";
        var conn = db.Database.GetDbConnection();

        // 2. Acquire transaction-owned Exclusive sp_getapplock on the exact same connection and transaction
        await using (var lockCmd = conn.CreateCommand())
        {
            lockCmd.Transaction = currentTx;
            lockCmd.CommandText = @"
                DECLARE @result INT;
                EXEC @result = sp_getapplock 
                    @Resource = @resource, 
                    @LockMode = 'Exclusive', 
                    @LockOwner = 'Transaction', 
                    @LockTimeout = 15000;
                SELECT @result;";

            var param = lockCmd.CreateParameter();
            param.ParameterName = "@resource";
            param.Value = $"CustomerReturnOrder_ReturnCode_{year}";
            lockCmd.Parameters.Add(param);

            var scalar = await lockCmd.ExecuteScalarAsync(cancellationToken);
            int lockResult = Convert.ToInt32(scalar);

            // 3. Negative return codes indicate timeout (-1), cancel (-2), deadlock victim (-3), or error (-999)
            if (lockResult < 0)
            {
                throw new InvalidOperationException($"Không thể lấy khóa ứng dụng để sinh mã đơn trả hàng cho năm {year} (Mã lỗi: {lockResult}). Vui lòng thử lại.");
            }
        }

        // 4. Query all return codes for the prefix under the lock to compute true numeric maximum in memory (avoids string sorting bugs)
        var codes = await db.CustomerReturnOrders.AsNoTracking()
            .Where(p => p.ReturnCode.StartsWith(prefix)).Select(p => p.ReturnCode).ToListAsync(cancellationToken);

        int nextSeq = 1;
        if (codes.Count > 0)
        {
            var maxSeq = codes
                .Select(c => int.TryParse(c.Replace(prefix, ""), out int s) ? s : 0)
                .DefaultIfEmpty(0)
                .Max();
            nextSeq = maxSeq + 1;
        }

        return $"{prefix}{nextSeq:D3}";
    }

    public async Task<T> InTransactionAsync<T>(int actorId, Func<Task<T>> work)
    {
        if (actorId <= 0) throw new ArgumentException("Yêu cầu thông tin tài khoản người dùng hợp lệ.", nameof(actorId));

        await db.Database.OpenConnectionAsync();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actorId}");
            var result = await work();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=NULL"); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    public async Task AcquireSalesOrderLineLocksAsync(IEnumerable<long> solineIds, CancellationToken cancellationToken = default)
    {
        var currentTx = db.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("Yêu cầu giao dịch đang hoạt động.");

        var orderedIds = solineIds.Distinct().OrderBy(id => id).ToList();
        var conn = db.Database.GetDbConnection();

        foreach (var id in orderedIds)
        {
            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.Transaction = currentTx;
                cmd.CommandText = "SELECT SOLineID FROM SalesOrderLine WITH (UPDLOCK, ROWLOCK) WHERE SOLineID = @solineId";
                var param = cmd.CreateParameter();
                param.ParameterName = "@solineId";
                param.Value = id;
                cmd.Parameters.Add(param);

                var scalar = await cmd.ExecuteScalarAsync(cancellationToken);
                if (scalar is null or DBNull)
                {
                    throw new InvalidOperationException($"Không tìm thấy dòng đơn bán hàng với ID {id}.");
                }
            }
            catch (SqlException ex) when (ex.Number == 1205)
            {
                throw new InvalidOperationException("Hệ thống đang bận xử lý giao dịch khác trên cùng dòng sản phẩm. Vui lòng thử lại sau giây lát.", ex);
            }
        }
    }

    public async Task SaveDraftAsync(
        CustomerReturnOrder order,
        IEnumerable<CustomerReturnOrderLine> lines,
        int actorId,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Thao tác lưu đơn trả hàng yêu cầu một giao dịch đang hoạt động (InTransactionAsync).");

        // Never trust client-supplied ReturnCode on Create; always generate server-side under the transaction lock
        order.ReturnCode = await GenerateReturnCodeAsync(cancellationToken);

        order.Status = "DRAFT";
        order.CreatedAt = DateTime.UtcNow;
        order.CreatedByUserId = actorId;

        db.CustomerReturnOrders.Add(order);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var line in lines)
        {
            line.ReturnOrderId = order.ReturnOrderId;
            line.SalesOrderId = order.SalesOrderId;
            line.CancelledQuantity = 0;
            db.CustomerReturnOrderLines.Add(line);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateDraftAsync(
        CustomerReturnOrder order,
        IEnumerable<CustomerReturnOrderLine> lines,
        byte[] originalRowVersion,
        int actorId,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Thao tác cập nhật đơn trả hàng yêu cầu một giao dịch đang hoạt động (InTransactionAsync).");

        if (originalRowVersion == null || originalRowVersion.Length == 0)
            throw new ArgumentException("Bắt buộc cung cấp originalRowVersion để chỉnh sửa đơn trả hàng.", nameof(originalRowVersion));

        // 1. Fetch current tracked entity
        var currentOrder = await db.CustomerReturnOrders.FirstOrDefaultAsync(o => o.ReturnOrderId == order.ReturnOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy đơn trả hàng với ID {order.ReturnOrderId}.");

        // 2. Revalidate current database status == DRAFT directly from tracked entity
        if (currentOrder.Status != "DRAFT")
        {
            throw new InvalidOperationException($"Chỉ đơn trả hàng ở trạng thái Bản nháp (DRAFT) mới được phép chỉnh sửa. Trạng thái hiện tại trong cơ sở dữ liệu: {currentOrder.Status}.");
        }

        // 3. Assign concurrency token original value directly to EF Core change tracker
        db.Entry(currentOrder).Property(e => e.RowVersion).OriginalValue = originalRowVersion;

        // 4. Only update approved editable fields, preserving immutable fields (SalesOrderId, ReturnCode, CreatedByUserId, CreatedAt)
        currentOrder.WarehouseId = order.WarehouseId;
        currentOrder.Reason = order.Reason;
        currentOrder.Status = "DRAFT";

        // 5. Force parent update so RowVersion advances even on line-only edits
        db.Entry(currentOrder).Property(x => x.Reason).IsModified = true;
        db.Entry(currentOrder).Property(x => x.WarehouseId).IsModified = true;

        // 6. Synchronize child lines by SolineId (Update existing, Insert new, Delete removed) preserving ReturnLineId
        await SynchronizeReturnLinesAsync(currentOrder.ReturnOrderId, currentOrder.SalesOrderId, lines, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new DbUpdateConcurrencyException("Dữ liệu đơn trả hàng đã được cập nhật bởi một người dùng khác. Vui lòng tải lại trang.", ex);
        }
    }

    public async Task ConfirmOrderAsync(
        CustomerReturnOrder order,
        IEnumerable<CustomerReturnOrderLine> lines,
        byte[]? originalRowVersion,
        int actorId,
        CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Thao tác xác nhận đơn trả hàng yêu cầu một giao dịch đang hoạt động (InTransactionAsync).");

        if (order.ReturnOrderId == 0)
        {
            // Case A: Create -> Save & Confirm
            // Never trust client-supplied ReturnCode on Create
            order.ReturnCode = await GenerateReturnCodeAsync(cancellationToken);
            order.Status = "OPEN";
            order.CreatedAt = DateTime.UtcNow;
            order.CreatedByUserId = actorId;

            db.CustomerReturnOrders.Add(order);
            await db.SaveChangesAsync(cancellationToken);

            foreach (var line in lines)
            {
                line.ReturnOrderId = order.ReturnOrderId;
                line.SalesOrderId = order.SalesOrderId;
                line.CancelledQuantity = 0;
                db.CustomerReturnOrderLines.Add(line);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Case B: Edit -> Save & Confirm
            if (originalRowVersion == null || originalRowVersion.Length == 0)
            {
                throw new ArgumentException("Bắt buộc cung cấp originalRowVersion để xác nhận đơn trả hàng bản nháp.", nameof(originalRowVersion));
            }

            // 1. Fetch current tracked entity
            var currentOrder = await db.CustomerReturnOrders.FirstOrDefaultAsync(o => o.ReturnOrderId == order.ReturnOrderId, cancellationToken)
                ?? throw new InvalidOperationException($"Không tìm thấy đơn trả hàng với ID {order.ReturnOrderId}.");

            // 2. Revalidate current database status == DRAFT directly from tracked entity
            if (currentOrder.Status != "DRAFT")
            {
                throw new InvalidOperationException($"Chỉ đơn trả hàng ở trạng thái Bản nháp (DRAFT) mới được phép xác nhận. Trạng thái hiện tại trong cơ sở dữ liệu: {currentOrder.Status}.");
            }

            // 3. Assign concurrency token original value directly to EF Core change tracker
            db.Entry(currentOrder).Property(e => e.RowVersion).OriginalValue = originalRowVersion;

            // 4. Update approved editable fields and set Status = OPEN, preserving immutable fields (SalesOrderId, ReturnCode, CreatedByUserId, CreatedAt)
            currentOrder.WarehouseId = order.WarehouseId;
            currentOrder.Reason = order.Reason;
            currentOrder.Status = "OPEN";

            // 5. Force parent update to advance RowVersion even if only child lines changed
            db.Entry(currentOrder).Property(x => x.Reason).IsModified = true;
            db.Entry(currentOrder).Property(x => x.Status).IsModified = true;
            db.Entry(currentOrder).Property(x => x.WarehouseId).IsModified = true;

            // 6. Synchronize child lines by SolineId (Update existing, Insert new, Delete removed)
            await SynchronizeReturnLinesAsync(currentOrder.ReturnOrderId, currentOrder.SalesOrderId, lines, cancellationToken);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new DbUpdateConcurrencyException("Dữ liệu đơn trả hàng đã được cập nhật bởi một người dùng khác. Vui lòng tải lại trang.", ex);
            }
        }
    }

    public async Task CancelOrderAsync(
        long returnOrderId,
        string? cancelReason,
        byte[]? originalRowVersion,
        int actorId,
        CancellationToken cancellationToken = default)
    {
        if (originalRowVersion is not { Length: 8 })
            throw new InvalidOperationException("Thông tin phiên bản dữ liệu không hợp lệ. Vui lòng tải lại trang trước khi hủy.");

        if (db.Database.CurrentTransaction == null)
            throw new InvalidOperationException("Thao tác hủy đơn trả hàng yêu cầu một giao dịch đang hoạt động (InTransactionAsync).");

        var currentOrder = await db.CustomerReturnOrders
            .Include(o => o.CustomerReturnOrderLineReturnOrders)
            .FirstOrDefaultAsync(o => o.ReturnOrderId == returnOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy đơn trả hàng với ID {returnOrderId}.");

        if (currentOrder.Status != "DRAFT" && currentOrder.Status != "OPEN")
        {
            throw new InvalidOperationException($"Chỉ đơn trả hàng ở trạng thái Bản nháp (DRAFT) hoặc Đã xác nhận (OPEN) mới được phép hủy. Trạng thái hiện tại: {currentOrder.Status}.");
        }

        bool hasReceipts = await HasReturnReceiptsAsync(returnOrderId, cancellationToken);
        if (hasReceipts)
        {
            throw new InvalidOperationException("Không thể hủy đơn trả hàng đã phát sinh phiếu nhận hàng (Inbound/ReturnReceipt).");
        }

        db.Entry(currentOrder).Property(e => e.RowVersion).OriginalValue = originalRowVersion;

        currentOrder.Status = "CANCELLED";
        if (!string.IsNullOrWhiteSpace(cancelReason))
        {
            var trimmed = cancelReason.Trim();
            currentOrder.Reason = string.IsNullOrWhiteSpace(currentOrder.Reason)
                ? $"[Lý do hủy]: {trimmed}"
                : $"{currentOrder.Reason} | [Lý do hủy]: {trimmed}";
            if (currentOrder.Reason.Length > 1000)
            {
                currentOrder.Reason = currentOrder.Reason[..1000];
            }
        }

        db.Entry(currentOrder).Property(x => x.Status).IsModified = true;
        db.Entry(currentOrder).Property(x => x.Reason).IsModified = true;

        foreach (var line in currentOrder.CustomerReturnOrderLineReturnOrders)
        {
            line.CancelledQuantity = line.RequestedQuantity;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new DbUpdateConcurrencyException("Dữ liệu đơn trả hàng đã được cập nhật bởi một người dùng khác. Vui lòng tải lại trang.", ex);
        }
    }

    private async Task SynchronizeReturnLinesAsync(
        long returnOrderId,
        long salesOrderId,
        IEnumerable<CustomerReturnOrderLine> newLines,
        CancellationToken cancellationToken)
    {
        var existingLines = await db.CustomerReturnOrderLines.Where(l => l.ReturnOrderId == returnOrderId).ToListAsync(cancellationToken);
        var existingMap = existingLines.GroupBy(l => l.SolineId).ToDictionary(g => g.Key, g => g.First());
        var inputMap = newLines.GroupBy(l => l.SolineId).ToDictionary(g => g.Key, g => g.Last());

        // A. Delete removed lines (present in DB, not present in input)
        var toDelete = existingLines.Where(el => !inputMap.ContainsKey(el.SolineId)).ToList();
        if (toDelete.Count > 0)
        {
            db.CustomerReturnOrderLines.RemoveRange(toDelete);
        }

        // B. Update existing lines (present in both DB and input) - preserves ReturnLineId
        foreach (var existingLine in existingLines.Where(el => inputMap.ContainsKey(el.SolineId)))
        {
            var inputLine = inputMap[existingLine.SolineId];
            existingLine.RequestedQuantity = inputLine.RequestedQuantity;
            existingLine.ProductId = inputLine.ProductId;
            existingLine.SalesOrderId = salesOrderId;
        }

        // C. Insert newly added lines (present in input, not present in DB)
        var toInsert = inputMap.Values.Where(il => !existingMap.ContainsKey(il.SolineId)).ToList();
        foreach (var inputLine in toInsert)
        {
            db.CustomerReturnOrderLines.Add(new CustomerReturnOrderLine
            {
                ReturnOrderId = returnOrderId,
                SalesOrderId = salesOrderId,
                SolineId = inputLine.SolineId,
                ProductId = inputLine.ProductId,
                RequestedQuantity = inputLine.RequestedQuantity,
                CancelledQuantity = 0
            });
        }
    }

    public async Task<(List<ReturnOrderListRowDto> Items, int TotalCount)> GetReturnOrdersPagedAsync(
        ReturnOrderFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = db.CustomerReturnOrders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(r => r.ReturnCode.Contains(s)
                || r.SalesOrder.Socode.Contains(s)
                || (r.SalesOrder.Customer != null && r.SalesOrder.Customer.CustomerName.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(r => r.Status == filter.Status);
        }

        if (filter.WarehouseId.HasValue)
        {
            query = query.Where(r => r.WarehouseId == filter.WarehouseId.Value);
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(r => r.CreatedAt >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(r => r.CreatedAt <= filter.ToUtc.Value);
        }

        if (filter.CreatedByUserId.HasValue)
        {
            query = query.Where(r => r.CreatedByUserId == filter.CreatedByUserId.Value);
        }

        if (filter.ExcludeDraft)
        {
            query = query.Where(r => r.Status != "DRAFT");
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var lastPageSkip = totalCount == 0 ? 0 : (totalCount - 1) / filter.Take * filter.Take;
        var skip = Math.Clamp(filter.Skip, 0, lastPageSkip);

        var items = await query
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.ReturnOrderId)
            .Skip(skip).Take(filter.Take)
            .Select(r => new ReturnOrderListRowDto(
                r.ReturnOrderId,
                r.ReturnCode,
                r.SalesOrder.Customer != null ? r.SalesOrder.Customer.CustomerName : "Khách lẻ",
                r.Warehouse.WarehouseName,
                r.CreatedAt,
                r.CreatedByUser.FullName,
                r.Status,
                r.CustomerReturnOrderLineReturnOrders.Sum(l => l.RequestedQuantity),
                db.ReturnReceiptItems.Count(rri => rri.IsActive && rri.ReturnReceipt.ReturnOrderId == r.ReturnOrderId && rri.ReturnReceipt.Status == "DONE"),
                db.ReturnReceipts.Any(rr => rr.ReturnOrderId == r.ReturnOrderId),
                r.RowVersion
            ))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<bool> HasReturnReceiptsAsync(long returnOrderId, CancellationToken cancellationToken = default) =>
        db.ReturnReceipts.AnyAsync(rr => rr.ReturnOrderId == returnOrderId, cancellationToken);

    public Task<List<WarehouseOptionDto>> GetAllWarehousesForFilterAsync(CancellationToken cancellationToken = default) =>
        db.Warehouses.AsNoTracking()
            .OrderBy(w => w.WarehouseName)
            .Select(w => new WarehouseOptionDto(
                w.WarehouseId,
                w.Status == "ACTIVE" ? w.WarehouseName : w.WarehouseName + " (Ngừng hoạt động)",
                w.Status
            ))
            .ToListAsync(cancellationToken);

    public virtual async Task<(int? WarehouseId, string? WarehouseName, string? RoleName)> GetUserRoleAndWarehouseAsync(
        int userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId && u.Status == "ACTIVE")
            .Select(u => new { u.WarehouseId, WarehouseName = u.Warehouse != null ? u.Warehouse.WarehouseName : null, RoleName = u.Role != null ? u.Role.RoleName : null })
            .FirstOrDefaultAsync(cancellationToken);
        return (user?.WarehouseId, user?.WarehouseName, user?.RoleName);
    }

    public virtual async Task<List<ReturnReceiptHistoryDto>> GetReturnReceiptHistoryAsync(
        long returnOrderId, CancellationToken cancellationToken = default)
    {
        return await db.ReturnReceipts.AsNoTracking()
            .Where(rr => rr.ReturnOrderId == returnOrderId)
            .OrderByDescending(rr => rr.CreatedAt)
            .Select(rr => new ReturnReceiptHistoryDto(
                rr.ReturnReceiptId,
                rr.ReceiptCode,
                rr.ReceiptDate,
                rr.Status,
                rr.CreatedAt,
                rr.CompletedAt,
                rr.PerformedByUser != null ? rr.PerformedByUser.FullName : rr.CreatedByUser.FullName,
                db.ReturnReceiptItems.Count(i => i.ReturnReceiptId == rr.ReturnReceiptId && i.IsActive),
                rr.Note
            ))
            .ToListAsync(cancellationToken);
    }

    public virtual async Task<Dictionary<long, int>> GetLineReceivedCountsAsync(
        long returnOrderId, CancellationToken cancellationToken = default)
    {
        return await db.ReturnReceiptItems.AsNoTracking()
            .Where(rri => rri.ReturnReceipt.ReturnOrderId == returnOrderId && rri.IsActive && rri.ReturnReceipt.Status == "DONE")
            .GroupBy(rri => rri.ReturnLineId)
            .Select(g => new { ReturnLineId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ReturnLineId, x => x.Count, cancellationToken);
    }
}
