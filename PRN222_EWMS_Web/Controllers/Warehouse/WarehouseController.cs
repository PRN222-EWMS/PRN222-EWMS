using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Services.Warehouse;
using PRN222_EWMS_Web.ViewModels.Warehouse;

namespace PRN222_EWMS_Web.Controllers.Warehouse;

[Authorize(Roles = "CEO,MANAGER")]
public class WarehouseController(IWarehouseService warehouses, ILogger<WarehouseController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] WarehouseListFilter filter, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(new WarehouseListViewModel(filter));

        try
        {
            return View(await warehouses.GetListAsync(filter, cancellationToken));
        }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            logger.LogError(ex, "Unable to load the warehouse list.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new WarehouseListViewModel(filter)
            {
                LoadError = "Không tải được danh sách kho. Vui lòng thử lại sau."
            });
        }
    }

    [HttpGet, Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return MissingWarehouse();
        try
        {
            var model = await warehouses.GetDetailsAsync(id, cancellationToken);
            return model is null ? MissingWarehouse() : View(model);
        }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            logger.LogError(ex, "Unable to load warehouse {WarehouseId} details.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Unavailable", "Không tải được chi tiết kho. Vui lòng thử lại sau.");
        }
    }

    [HttpGet, Authorize(Roles = "MANAGER")]
    public IActionResult Create() => View("Form", new WarehouseFormViewModel());

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Create(WarehouseFormViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Form", model);
        try
        {
            var result = await warehouses.CreateAsync(model, cancellationToken);
            if (result.WarehouseId is int id)
            {
                TempData["Success"] = "Đã lưu kho. Tiếp tục thêm Zone và cấu hình các vị trí lưu trữ.";
                return RedirectToAction(nameof(Setup), new { id });
            }
            AddErrors(result);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException or TimeoutException)
        {
            logger.LogError(ex, "Unable to create warehouse.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ModelState.AddModelError("", "Không lưu được kho. Vui lòng thử lại sau.");
        }
        return View("Form", model);
    }

    [HttpGet, Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return MissingWarehouse();
        try
        {
            var model = await warehouses.GetEditAsync(id, cancellationToken);
            return model is null ? MissingWarehouse() : View("Form", model);
        }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            logger.LogError(ex, "Unable to load warehouse {WarehouseId} for editing.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Unavailable", "Không tải được thông tin kho để chỉnh sửa. Vui lòng thử lại sau.");
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Edit([FromRoute] int id, WarehouseFormViewModel model, CancellationToken cancellationToken)
    {
        if (id <= 0) return MissingWarehouse();
        model.WarehouseId = id;
        try
        {
            if (ModelState.IsValid)
            {
                var result = await warehouses.UpdateAsync(id, model, cancellationToken);
                if (result.NotFound) return MissingWarehouse();
                if (result.WarehouseId.HasValue)
                {
                    TempData["Success"] = "Đã cập nhật kho thành công.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                AddErrors(result);
                if (result.Conflict) Response.StatusCode = StatusCodes.Status409Conflict;
            }
            var current = await warehouses.GetEditAsync(id, cancellationToken);
            if (current is null) return MissingWarehouse();
            model.IsOccupied = current.IsOccupied;
            model.IsLocked = current.IsLocked;
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException or TimeoutException)
        {
            logger.LogError(ex, "Unable to update warehouse {WarehouseId}.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ModelState.AddModelError("", "Không lưu được thay đổi. Vui lòng thử lại sau.");
        }
        return View("Form", model);
    }

    [HttpGet, Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> Setup(int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return MissingWarehouse();
        try
        {
            var model = await warehouses.GetDetailsAsync(id, cancellationToken);
            if (model is null) return MissingWarehouse();
            ViewData["Configuring"] = true;
            return View("Details", model);
        }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            logger.LogError(ex, "Unable to load warehouse {WarehouseId} setup.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Unavailable", "Không tải được cấu trúc kho. Vui lòng thử lại sau.");
        }
    }

    [HttpGet, Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> ConfigureLocation(int id, int? locationId, int? parentId, CancellationToken cancellationToken)
    {
        if (id <= 0 || locationId is <= 0 || parentId is <= 0) return MissingLocation();
        try
        {
            var model = await warehouses.GetLocationFormAsync(id, locationId, parentId, cancellationToken);
            return model is null ? MissingLocation() : View("LocationForm", model);
        }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            logger.LogError(ex, "Unable to load location configuration in warehouse {WarehouseId}.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Unavailable", "Không tải được cấu hình vị trí. Vui lòng thử lại sau.");
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> ConfigureLocation([FromRoute] int id, [FromQuery] int? locationId,
        StorageLocationFormViewModel model, CancellationToken cancellationToken)
    {
        if (id <= 0 || locationId is <= 0) return MissingLocation();
        model.WarehouseId = id;
        model.LocationId = locationId ?? 0;
        try
        {
            if (ModelState.IsValid)
            {
                var result = await warehouses.SaveLocationAsync(id, locationId, model, cancellationToken);
                if (result.NotFound) return MissingLocation();
                if (result.LocationId.HasValue)
                {
                    TempData["Success"] = locationId.HasValue
                        ? "Đã cập nhật vị trí lưu trữ."
                        : "Đã thêm vị trí lưu trữ. Tiếp tục cấu hình các vị trí con nếu cần.";
                    return RedirectToAction(nameof(Setup), new { id });
                }
                AddErrors(result);
                if (result.Conflict) Response.StatusCode = StatusCodes.Status409Conflict;
            }
            await warehouses.PopulateLocationOptionsAsync(model, cancellationToken);
            return View("LocationForm", model);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException or TimeoutException)
        {
            logger.LogError(ex, "Unable to save location configuration in warehouse {WarehouseId}.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ModelState.AddModelError("", "Không lưu được cấu hình vị trí. Vui lòng thử lại sau.");
            try
            {
                await warehouses.PopulateLocationOptionsAsync(model, cancellationToken);
                return View("LocationForm", model);
            }
            catch (Exception reloadException) when (reloadException is DbException or TimeoutException)
            {
                logger.LogError(reloadException, "Unable to reload location configuration options in warehouse {WarehouseId}.", id);
                return View("Unavailable", "Không tải được cấu hình vị trí. Vui lòng thử lại sau.");
            }
        }
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "MANAGER")]
    public async Task<IActionResult> CompleteSetup([FromRoute] int id, CancellationToken cancellationToken)
    {
        if (id <= 0) return MissingWarehouse();
        try
        {
            var model = await warehouses.GetDetailsAsync(id, cancellationToken);
            if (model is null) return MissingWarehouse();
            if (model.SetupIssues.Count > 0)
            {
                ViewData["Configuring"] = true;
                Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                ModelState.AddModelError("", "Cấu trúc kho chưa đầy đủ. Vui lòng xử lý các mục được liệt kê trước khi hoàn tất.");
                return View("Details", model);
            }
            // Each configuration step is already persisted independently; completion only returns to the overview.
            TempData["Success"] = "Đã hoàn tất cấu hình cấu trúc kho.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex) when (ex is DbException or TimeoutException)
        {
            logger.LogError(ex, "Unable to complete warehouse {WarehouseId} setup.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View("Unavailable", "Không tải được cấu trúc kho để hoàn tất. Vui lòng thử lại sau.");
        }
    }

    private IActionResult MissingLocation()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("Unavailable", "Kho hoặc vị trí không tồn tại, hoặc vị trí không thuộc cấu trúc kho này.");
    }

    private IActionResult MissingWarehouse()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("Unavailable", "Kho không tồn tại hoặc đã bị xóa.");
    }

    private void AddErrors(WarehouseSaveResult result)
    {
        foreach (var (field, error) in result.Errors) ModelState.AddModelError(field, error);
    }
}
