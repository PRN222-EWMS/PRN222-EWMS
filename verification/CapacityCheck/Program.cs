using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Services.Purchasing;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.Services.Sales;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Controllers.Warehouse;
using PRN222_EWMS_Web.Repositories.Sales;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.Services;
using Microsoft.Extensions.Logging.Abstractions;

var connection = Environment.GetEnvironmentVariable("EWMS_TEST_CONNECTION")
    ?? "Server=localhost;Database=WMSDB;Integrated Security=True;TrustServerCertificate=True";
var options = new DbContextOptionsBuilder<EwmsDbContext>().UseSqlServer(connection).Options;
await using var db = new EwmsDbContext(options);
var clock = TimeProvider.System;
var engine = new WarehouseCapacityEngine(db, new SourceReservationService(db, clock), clock);
var draft = await db.CustomerReturnOrders.AsNoTracking()
    .Where(o => o.Status == "DRAFT" && o.WarehouseId == 1)
    .OrderBy(o => o.ReturnOrderId).FirstAsync();
var lines = await db.CustomerReturnOrderLines.AsNoTracking()
    .Where(l => l.ReturnOrderId == draft.ReturnOrderId)
    .Select(l => new { l.ProductId, l.RequestedQuantity, l.CancelledQuantity })
    .ToListAsync();
var demands = lines.Select(l => new CapacityDemand(l.ProductId, l.RequestedQuantity - l.CancelledQuantity, "UNKNOWN")).ToList();
var result = await engine.EvaluateAsync(new WarehouseCapacityRequest
{
    WarehouseId = draft.WarehouseId, ExcludeReturnOrderId = draft.ReturnOrderId, Demands = demands
});
Console.WriteLine($"Draft {draft.ReturnCode}: valid={result.IsValid}; physicalFits={result.PhysicalFits}; warnings={string.Join(" | ", result.Warnings)}; errors={string.Join(" | ", result.Errors)}");
if (!result.IsValid) throw new Exception("Existing return draft still fails capacity evaluation");
var service = new CustomerReturnOrderService(null!, null!, engine);
var checkMethod = typeof(CustomerReturnOrderService).GetMethod("CheckReturnCapacityAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
var orderLines = lines.Select(l => new CustomerReturnOrderLine
{
    ProductId = l.ProductId, RequestedQuantity = l.RequestedQuantity, CancelledQuantity = l.CancelledQuantity
}).ToList();
await (Task)checkMethod.Invoke(service, [draft.WarehouseId, orderLines, draft.ReturnOrderId, CancellationToken.None])!;
Console.WriteLine("SaveAndConfirm capacity gate passed for existing draft");
var damaged = await engine.EvaluateAsync(new WarehouseCapacityRequest
{
    WarehouseId = draft.WarehouseId, ExcludeReturnOrderId = draft.ReturnOrderId,
    Demands = demands.Select(d => d with { Condition = "DAMAGED" }).ToList()
});
Console.WriteLine($"Actual damaged receipt: valid={damaged.IsValid}; errors={string.Join(" | ", damaged.Errors)}");
if (damaged.IsValid) throw new Exception("Damaged receipt must fail without a DAMAGED slot");
Console.WriteLine("Live WMSDB capacity checks passed");
var sources = new SourceReservationService(db, clock);
var optionsForSales = await sources.GetOptionsAsync(1, null);
Console.WriteLine($"Sales order source query passed: {optionsForSales.Count} options");
var codes = new DocumentCodeGenerator(db, clock);
var sales = new PRN222_EWMS_Web.Services.Sales.SalesService(
    new SalesRepository(db, codes), new UserRepository(db),
    new PRN222_EWMS_Web.Services.Sales.SalesOrderSourceService(db, sources, clock,
        NullLogger<PRN222_EWMS_Web.Services.Sales.SalesOrderSourceService>.Instance, codes), sources);
var createForm = await sales.GetCreateFormAsync(0);
if (createForm.Warehouses.Count == 0 || createForm.Customers.Count == 0)
    throw new Exception("SalesOrder/Create form did not load warehouse/customer data");
Console.WriteLine($"SalesOrder/Create form passed: {createForm.Warehouses.Count} warehouses, {createForm.Customers.Count} customers");
foreach (var (role, destination) in new[] { ("WAREHOUSE_STAFF", "WarehouseTransfer"), ("MANAGER", "ManagerTransfer") })
{
    var controller = new TransferController(null!)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"))
            }
        }
    };
    var redirect = controller.Index() as RedirectToActionResult;
    if (redirect?.ControllerName != destination || redirect.ActionName != "Index")
        throw new Exception($"/Transfer route failed for {role}");
    Console.WriteLine($"/Transfer {role} -> {destination}/Index passed");
}
