using Microsoft.EntityFrameworkCore;
using System.Data;
using PRN222_EWMS_Web.Models;
namespace PRN222_EWMS_Web.Repositories.WarehouseStaff;
public class ReturnReceiveRepository(EwmsDbContext db):IReturnReceiveRepository
{
 public Task<int?> WarehouseAsync(int userId)=>db.Users.AsNoTracking().Where(u=>u.UserId==userId&&u.Status=="ACTIVE"&&u.Role.RoleName=="WAREHOUSE_STAFF").Select(u=>u.WarehouseId).SingleOrDefaultAsync();
 private IQueryable<CustomerReturnOrder> Filter(int warehouseId,string? search){var q=db.CustomerReturnOrders.AsNoTracking().Where(o=>o.WarehouseId==warehouseId);return search is null?q:q.Where(o=>o.ReturnCode.Contains(search)||o.SalesOrder.Socode.Contains(search));}
 public Task<int> CountOrdersAsync(int w,string? s)=>Filter(w,s).CountAsync();
 public Task<List<CustomerReturnOrder>> OrdersAsync(int w,string? s,int skip,int take)=>Filter(w,s).Include(o=>o.SalesOrder).ThenInclude(so=>so.Customer).Include(o=>o.SalesOrder).ThenInclude(so=>so.Outbounds).Include(o=>o.CustomerReturnOrderLineReturnOrders).OrderByDescending(o=>o.CreatedAt).Skip(skip).Take(take).AsSplitQuery().ToListAsync();
 public Task<List<ReturnReceipt>> ReceiptsAsync(int w)=>db.ReturnReceipts.AsNoTracking().Where(r=>r.ReturnOrder.WarehouseId==w).Include(r=>r.ReturnOrder).Include(r=>r.ReturnReceiptItemReturnReceipts).OrderByDescending(r=>r.CreatedAt).Take(100).ToListAsync();
 public async Task<Dictionary<long,int>> ReceivedCountsAsync(IEnumerable<long> orderIds){var ids=orderIds.ToArray();return await db.ReturnReceiptItems.AsNoTracking().Where(i=>ids.Contains(i.ReturnOrderId)&&i.IsActive&&i.ReturnReceipt.Status=="DONE").GroupBy(i=>i.ReturnOrderId).Select(g=>new {Id=g.Key,Count=g.Count()}).ToDictionaryAsync(x=>x.Id,x=>x.Count);}
 public Task<CustomerReturnOrder?> OrderAsync(long id,int w)=>db.CustomerReturnOrders.Include(o=>o.SalesOrder).ThenInclude(so=>so.Customer).Include(o=>o.SalesOrder).ThenInclude(so=>so.Outbounds).Include(o=>o.CustomerReturnOrderLineReturnOrders).Include(o=>o.ReturnReceiptReturnOrders).ThenInclude(r=>r.ReturnReceiptItemReturnReceipts).AsSplitQuery().SingleOrDefaultAsync(o=>o.ReturnOrderId==id&&o.WarehouseId==w);
 public Task<ReturnReceipt?> ReceiptAsync(long id,int w)=>db.ReturnReceipts.Include(r=>r.ReturnOrder).ThenInclude(o=>o.SalesOrder).ThenInclude(so=>so.Customer).Include(r=>r.ReturnOrder).ThenInclude(o=>o.CustomerReturnOrderLineReturnOrders).Include(r=>r.ReturnReceiptItemReturnReceipts).ThenInclude(i=>i.OutboundItem).ThenInclude(o=>o.SerialItem).ThenInclude(s=>s.Product).AsSplitQuery().SingleOrDefaultAsync(r=>r.ReturnReceiptId==id&&r.ReturnOrder.WarehouseId==w);
 public Task<OutboundItem?> SoldSerialAsync(long salesOrderId,string serial)=>db.OutboundItems.Include(i=>i.Outbound).Include(i=>i.SerialItem).ThenInclude(s=>s.Product).SingleOrDefaultAsync(i=>i.IsActive&&i.SalesOrderId==salesOrderId&&i.Outbound.Status=="DONE"&&i.SerialItem.SerialNumber==serial);
 public Task<InboundItem?> OriginalInboundItemAsync(long serialItemId)=>db.InboundItems.AsNoTracking().Where(i=>i.IsActive&&i.SerialItemId==serialItemId).OrderByDescending(i=>i.ScannedAt).FirstOrDefaultAsync();
 public Task<bool> AlreadyReturnedAsync(long id,long? exclude=null)=>db.ReturnReceiptItems.AnyAsync(i=>i.IsActive&&i.OutboundItemId==id&&i.ReturnItemId!=exclude);
 public Task<int> ReceivedLineCountAsync(long id,long? exclude=null)=>db.ReturnReceiptItems.CountAsync(i=>i.IsActive&&i.ReturnLineId==id&&i.ReturnItemId!=exclude&&i.ReturnReceipt.Status!="CANCELLED");
 public async Task<T> TransactionAsync<T>(int actor,Func<Task<T>> work){await db.Database.OpenConnectionAsync();try{await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);await db.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_set_session_context @key=N'ActorUserID', @value={actor}");var result=await work();await db.SaveChangesAsync();await tx.CommitAsync();return result;}finally{try{await db.Database.ExecuteSqlRawAsync("EXEC sys.sp_set_session_context @key=N'ActorUserID', @value=NULL");}finally{await db.Database.CloseConnectionAsync();}}}
 public Task SaveAsync()=>db.SaveChangesAsync();
 public void AddReceipt(ReturnReceipt r)=>db.ReturnReceipts.Add(r);
 public void AddItem(ReturnReceiptItem i)=>db.ReturnReceiptItems.Add(i);
 public async Task PostAsync(ReturnReceipt receipt,int actor)
 {
  var now=DateTime.UtcNow;
  var warehouseId=receipt.ReturnOrder.WarehouseId;
  foreach(var group in receipt.ReturnReceiptItemReturnReceipts.Where(i=>i.IsActive).GroupBy(i=>new {i.OutboundItem.ProductId,Condition=i.ReceivedCondition!}))
  {
   var good=group.Key.Condition=="GOOD";
   var source=new SupplySource{ProductId=group.Key.ProductId,WarehouseId=warehouseId,ItemCondition=group.Key.Condition,SourceType="RETURN",SupplyState="PHYSICAL",ReturnReceiptId=receipt.ReturnReceiptId,EligibleQuantity=good?group.Count():0,RemainingQuantity=good?group.Count():0,AvailableFrom=DateOnly.FromDateTime(now),CreatedAt=now};
   db.SupplySources.Add(source);await db.SaveChangesAsync();
   foreach(var item in group)
   {
    var serial=item.OutboundItem.SerialItem;
    var movement=new SerialMovement{SerialItemId=serial.SerialItemId,PerformedByUserId=actor,EventType="CUSTOMER_RETURN",OccurredAt=now,FromState=serial.StockState,ToState="IN_WAREHOUSE",FromCondition=serial.ItemCondition,ToCondition=group.Key.Condition,FromWarehouseId=serial.CurrentWarehouseId,ToWarehouseId=warehouseId,FromLocationId=serial.CurrentLocationId,FromQuarantined=serial.IsQuarantined,ToQuarantined=!good,ReturnReceiptId=receipt.ReturnReceiptId};
    serial.CurrentWarehouseId=warehouseId;serial.CurrentLocationId=null;serial.CurrentSupplySourceId=source.SupplySourceId;serial.CurrentSupplyState="PHYSICAL";serial.StockState="IN_WAREHOUSE";serial.ItemCondition=group.Key.Condition;serial.IsQuarantined=!good;serial.QuarantineReason=good?null:item.ConditionNote;
    db.SerialMovements.Add(movement);
   }
  }
  await db.SaveChangesAsync();
  receipt.Status="DONE";receipt.CompletedAt=now;receipt.PerformedByUserId=actor;
  await db.SaveChangesAsync();
 }
}
