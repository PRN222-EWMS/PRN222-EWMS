using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Supplier;
using PRN222_EWMS_Web.ViewModels.Supplier;

namespace PRN222_EWMS_Web.Controllers;

[Authorize(Roles = "PURCHASING_STAFF")]
public class SupplierController(ISupplierService supplierService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SupplierListFilter filter)
    {
        var model = await supplierService.GetSupplierListAsync(filter);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, [FromQuery] SupplierListFilter returnFilter)
    {
        var success = await supplierService.ToggleStatusAsync(id);
        if (!success)
        {
            TempData["Error"] = "Không tìm thấy nhà cung cấp để cập nhật trạng thái.";
        }
        else
        {
            TempData["Success"] = "Đã cập nhật trạng thái nhà cung cấp thành công.";
        }

        return RedirectToAction(nameof(Index), new
        {
            search = returnFilter.Search,
            status = returnFilter.Status,
            page = returnFilter.Page
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, [FromQuery] SupplierListFilter returnFilter)
    {
        var model = await supplierService.GetSupplierDetailsAsync(id);
        if (model is null)
        {
            TempData["Error"] = "Không tìm thấy nhà cung cấp.";
            return RedirectToAction(nameof(Index), new
            {
                search = returnFilter.Search,
                status = returnFilter.Status,
                page = returnFilter.Page
            });
        }

        model.ReturnFilter = returnFilter;
        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateSupplierViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateSupplierViewModel model)
    {
        if (ModelState.IsValid)
        {
            var (supplierId, errors) = await supplierService.CreateAsync(model);
            foreach (var (key, value) in errors)
            {
                ModelState.AddModelError(key, value);
            }

            if (errors.Count == 0 && supplierId.HasValue)
            {
                TempData["Success"] = "Đã tạo nhà cung cấp mới thành công.";
                return RedirectToAction(nameof(Details), new { id = supplierId.Value });
            }
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, [FromQuery] SupplierListFilter returnFilter)
    {
        var model = await supplierService.GetEditSupplierAsync(id, returnFilter);
        if (model is null)
        {
            TempData["Error"] = "Không tìm thấy nhà cung cấp.";
            return RedirectToAction(nameof(Index), new
            {
                search = returnFilter.Search,
                status = returnFilter.Status,
                page = returnFilter.Page
            });
        }

        // Q55: Server must reject access if INACTIVE
        if (model.Status != "ACTIVE")
        {
            TempData["Error"] = "Nhà cung cấp đang ở trạng thái ngừng hoạt động và không thể chỉnh sửa. Vui lòng kích hoạt lại trước.";
            return RedirectToAction(nameof(Index), new
            {
                search = returnFilter.Search,
                status = returnFilter.Status,
                page = returnFilter.Page
            });
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditSupplierViewModel model)
    {
        if (id != model.SupplierId)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            var errors = await supplierService.UpdateAsync(id, model);
            foreach (var (key, value) in errors)
            {
                ModelState.AddModelError(key, value);
            }

            if (errors.Count == 0)
            {
                TempData["Success"] = "Đã cập nhật thông tin nhà cung cấp thành công.";
                return RedirectToAction(nameof(Details), new
                {
                    id = model.SupplierId,
                    search = model.ReturnFilter.Search,
                    status = model.ReturnFilter.Status,
                    page = model.ReturnFilter.Page
                });
            }
        }

        return View(model);
    }
}

