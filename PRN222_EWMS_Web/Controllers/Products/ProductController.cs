using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN222_EWMS_Web.Services.Products;
using PRN222_EWMS_Web.ViewModels.Products;

namespace PRN222_EWMS_Web.Controllers.Products;

[Authorize(Roles = "MANAGER,PURCHASING_STAFF,WAREHOUSE_STAFF")]
public class ProductController(IProductService service) : Controller
{
    private int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsWarehouseStaff =>
        User.IsInRole("WAREHOUSE_STAFF");

    [HttpGet]
    public async Task<IActionResult> Index(string? keyword, int? warehouseId)
    {
        if (!ModelState.IsValid)
            return BadRequest();

        var result = await service.GetProductListPageAsync(
            keyword, warehouseId, CurrentUserId,
            User.IsInRole("MANAGER"), IsWarehouseStaff,
            User.IsInRole("PURCHASING_STAFF"));

        return result is null ? NotFound() : View(result);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await service.GetDetailAsync(
            id, CurrentUserId, IsWarehouseStaff);

        return product is null ? NotFound() : View(product);
    }

    [HttpGet]
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Create()
    {
        var model = new ProductCreateViewModel();

        await service.LoadCreateOptionsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Create(
    [FromForm] ProductCreateViewModel input)
    {
        if (ModelState.IsValid)
        {
            var result = await service.CreateAsync(input, CurrentUserId);

            if (result.Id.HasValue)
            {
                TempData["Success"] = "Tạo sản phẩm thành công.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = result.Id.Value });
            }

            ModelState.AddModelError(
                result.Field ?? "",
                result.Error ?? "Không thể tạo sản phẩm.");
        }

        await service.LoadCreateOptionsAsync(input);

        return View(input);
    }

    [HttpGet]
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await service.GetEditAsync(id);

        return model is null ? NotFound() : View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "PURCHASING_STAFF")]
    public async Task<IActionResult> Edit(
    [FromRoute] int id,
    [FromForm] ProductEditViewModel input)
    {
        if (id != input.ProductId)
            return BadRequest();

        if (ModelState.IsValid)
        {
            var result = await service.UpdateAsync(id, input);

            if (result.Success)
            {
                TempData["Success"] = "Cập nhật sản phẩm thành công.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }

            ModelState.AddModelError(
                result.Field ?? "",
                result.Error ?? "Không thể cập nhật sản phẩm.");
        }

        await service.LoadCreateOptionsAsync(input);

        return View(input);
    }
}

