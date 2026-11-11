using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.WarehouseStaff;
namespace PRN222_EWMS_Web.Controllers.WarehouseStaff;
[Authorize(Roles="WAREHOUSE_STAFF")]
public class ReturnReceiveController(IReturnReceiveService service,ILogger<ReturnReceiveController> logger):Controller
{
 private int UserId=>int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
 [HttpGet] public async Task<IActionResult> Index(string? search,int page=1)=>View(await service.ListAsync(UserId,search,page));
 [HttpGet] public async Task<IActionResult> Details(long id){var model=await service.DetailAsync(UserId,id);return model is null?NotFound():View(model);}
 [HttpGet] public async Task<IActionResult> PreviewSerial(long id,string serial){try{return Json(new{ok=true,data=await service.PreviewSerialAsync(UserId,id,serial)});}catch(Exception e)when(e is ArgumentException or InvalidOperationException){return BadRequest(new{ok=false,message=e.Message});}}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Start(long returnOrderId,long outboundId){try{return RedirectToAction(nameof(Details),new{id=await service.StartAsync(UserId,returnOrderId,outboundId)});}catch(Exception e)when(e is ArgumentException or InvalidOperationException){TempData["Error"]=e.Message;return RedirectToAction(nameof(Index));}}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> AddSerial(long id,string serial,string condition,string? note,string version)
 {
  var ajax=Request.Headers["X-Requested-With"]=="XMLHttpRequest";
  try
  {
   await service.AddSerialAsync(UserId,id,serial,condition,note,version);
   if(ajax)
   {
    var detail=await service.DetailAsync(UserId,id);
    if(detail is null)return NotFound();
    Response.Headers["X-Receipt-Version"]=detail.Version;
    Response.Headers["X-Received-Count"]=detail.Received.ToString();
    return PartialView("_SerialList",detail);
   }
   TempData["Success"]="Đã thêm Serial vào phiếu nháp.";
  }
  catch(Exception e)when(e is ArgumentException or InvalidOperationException or Microsoft.EntityFrameworkCore.DbUpdateException)
  {
   logger.LogWarning(e,"Return receipt scan failed {ReceiptId}",id);
   var message=e is Microsoft.EntityFrameworkCore.DbUpdateException?"Database từ chối lưu Serial. Kiểm tra lại phiếu xuất và Serial.":e.Message;
   if(ajax)return BadRequest(new{message});
   TempData["Error"]=message;
  }
  return RedirectToAction(nameof(Details),new{id});
 }
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> RemoveSerial(long id,long itemId,string version){try{await service.RemoveSerialAsync(UserId,id,itemId,version);TempData["Success"]="Đã hủy Serial nháp.";}catch(Exception e)when(e is ArgumentException or InvalidOperationException){TempData["Error"]=e.Message;}return RedirectToAction(nameof(Details),new{id});}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Complete(long id,string version){try{await service.CompleteAsync(UserId,id,version);TempData["Success"]="Đã hoàn tất nhận hàng trả.";}catch(Exception e)when(e is ArgumentException or InvalidOperationException or Microsoft.EntityFrameworkCore.DbUpdateException){logger.LogWarning(e,"Return receipt completion failed {ReceiptId}",id);TempData["Error"]=e is Microsoft.EntityFrameworkCore.DbUpdateException?"Database từ chối hoàn tất phiếu nhận trả. Kiểm tra log để biết ràng buộc cụ thể.":e.Message;}return RedirectToAction(nameof(Details),new{id});}
 [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Cancel(long id,string version){try{await service.CancelAsync(UserId,id,version);TempData["Success"]="Đã hủy phiếu nhận trả.";}catch(Exception e)when(e is ArgumentException or InvalidOperationException){TempData["Error"]=e.Message;}return RedirectToAction(nameof(Details),new{id});}
}
