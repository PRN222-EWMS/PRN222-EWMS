using System.Text.RegularExpressions;
using PRN222_EWMS_Web.Repositories.Supplier;
using PRN222_EWMS_Web.ViewModels.Supplier;
using SupplierEntity = PRN222_EWMS_Web.Models.Supplier;

namespace PRN222_EWMS_Web.Services.Supplier;

public class SupplierService(ISupplierRepository repo) : ISupplierService
{
    private const int DefaultPageSize = 10;
    private static readonly string[] AllowedStatuses = ["ACTIVE", "INACTIVE", "ALL"];
    private static readonly Regex PhoneRegex = new(@"^0[0-9]{9}$", RegexOptions.Compiled);

    public async Task<SupplierListViewModel> GetSupplierListAsync(SupplierListFilter filter)
    {
        var search = string.IsNullOrWhiteSpace(filter.Search) ? null : filter.Search.Trim();
        var status = !string.IsNullOrWhiteSpace(filter.Status) && AllowedStatuses.Contains(filter.Status.ToUpper())
            ? filter.Status.ToUpper()
            : "ACTIVE";

        var page = Math.Max(1, filter.Page);
        var (items, totalCount, normalizedPage, totalPages) = await repo.GetPageAsync(search, status, page, DefaultPageSize);

        return new SupplierListViewModel
        {
            Search = search,
            Status = status,
            Page = normalizedPage,
            PageSize = DefaultPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Suppliers = items.Select(s => new SupplierListItemViewModel
            {
                SupplierId = s.SupplierId,
                SupplierName = s.SupplierName,
                Nickname = s.Nickname,
                Phone = s.Phone,
                Email = s.Email,
                Status = s.Status
            }).ToList()
        };
    }

    public async Task<SupplierDetailsViewModel?> GetSupplierDetailsAsync(int supplierId)
    {
        var supplier = await repo.FindByIdAsync(supplierId);
        if (supplier is null) return null;

        return new SupplierDetailsViewModel
        {
            SupplierId = supplier.SupplierId,
            SupplierName = supplier.SupplierName,
            Nickname = supplier.Nickname,
            Address = supplier.Address,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Status = supplier.Status
        };
    }

    public async Task<EditSupplierViewModel?> GetEditSupplierAsync(int supplierId, SupplierListFilter? returnFilter = null)
    {
        var supplier = await repo.FindByIdAsync(supplierId);
        if (supplier is null) return null;

        return new EditSupplierViewModel
        {
            SupplierId = supplier.SupplierId,
            SupplierName = supplier.SupplierName,
            Nickname = supplier.Nickname,
            Address = supplier.Address,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Status = supplier.Status,
            ReturnFilter = returnFilter ?? new SupplierListFilter()
        };
    }

    public async Task<(int? SupplierId, Dictionary<string, string> Errors)> CreateAsync(CreateSupplierViewModel model)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var name = CleanString(model.SupplierName);
        var nickname = CleanString(model.Nickname);
        var address = CleanString(model.Address);
        var phone = CleanString(model.Phone);
        var email = CleanString(model.Email);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameof(model.SupplierName)] = "Tên nhà cung cấp không được để trống.";
        }

        if (!string.IsNullOrWhiteSpace(email) && await repo.EmailExistsAsync(email))
        {
            errors[nameof(model.Email)] = "Email này đã được sử dụng bởi một nhà cung cấp khác.";
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (!PhoneRegex.IsMatch(phone))
            {
                errors[nameof(model.Phone)] = "Số điện thoại không hợp lệ (phải gồm đúng 10 chữ số và bắt đầu bằng số 0).";
            }
            else if (await repo.PhoneExistsAsync(phone))
            {
                model.HasDuplicatePhoneWarning = true;
                if (!model.ConfirmDuplicatePhone)
                {
                    errors[nameof(model.ConfirmDuplicatePhone)] = "Số điện thoại này đã tồn tại ở một nhà cung cấp khác. Vui lòng tích chọn xác nhận tiếp tục sử dụng số này nếu muốn lưu.";
                }
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        var supplier = new SupplierEntity
        {
            SupplierName = name!,
            Nickname = nickname,
            Address = address,
            Phone = phone,
            Email = email,
            Status = "ACTIVE"
        };

        await repo.AddAsync(supplier);
        return (supplier.SupplierId, errors);
    }

    public async Task<Dictionary<string, string>> UpdateAsync(int supplierId, EditSupplierViewModel model)
    {
        var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var supplier = await repo.FindByIdAsync(supplierId);
        if (supplier is null)
        {
            errors["General"] = "Không tìm thấy nhà cung cấp.";
            return errors;
        }

        // Q52, Q61: Only ACTIVE suppliers can have their information edited.
        if (supplier.Status != "ACTIVE")
        {
            errors["Status"] = "Nhà cung cấp đang ở trạng thái ngừng hoạt động và không thể chỉnh sửa. Vui lòng kích hoạt lại trước.";
            return errors;
        }

        var name = CleanString(model.SupplierName);
        var nickname = CleanString(model.Nickname);
        var address = CleanString(model.Address);
        var phone = CleanString(model.Phone);
        var email = CleanString(model.Email);

        if (string.IsNullOrWhiteSpace(name))
        {
            errors[nameof(model.SupplierName)] = "Tên nhà cung cấp không được để trống.";
        }

        // Q42: If email is unchanged, allow save even if duplicate in historical seed.
        if (!string.IsNullOrWhiteSpace(email))
        {
            var isEmailChanged = !string.Equals(supplier.Email?.Trim(), email, StringComparison.OrdinalIgnoreCase);
            if (isEmailChanged && await repo.EmailExistsAsync(email, supplierId))
            {
                errors[nameof(model.Email)] = "Email này đã được sử dụng bởi một nhà cung cấp khác.";
            }
        }

        // Q30: If phone is unchanged, do not warn / require checkbox.
        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (!PhoneRegex.IsMatch(phone))
            {
                errors[nameof(model.Phone)] = "Số điện thoại không hợp lệ (phải gồm đúng 10 chữ số và bắt đầu bằng số 0).";
            }
            else
            {
                var isPhoneChanged = !string.Equals(supplier.Phone?.Trim(), phone, StringComparison.Ordinal);
                if (isPhoneChanged && await repo.PhoneExistsAsync(phone, supplierId))
                {
                    model.HasDuplicatePhoneWarning = true;
                    if (!model.ConfirmDuplicatePhone)
                    {
                        errors[nameof(model.ConfirmDuplicatePhone)] = "Số điện thoại này đã tồn tại ở một nhà cung cấp khác. Vui lòng tích chọn xác nhận tiếp tục sử dụng số này nếu muốn lưu.";
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        supplier.SupplierName = name!;
        supplier.Nickname = nickname;
        supplier.Address = address;
        supplier.Phone = phone;
        supplier.Email = email;
        supplier.Status = string.Equals(model.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase) ? "INACTIVE" : "ACTIVE";

        await repo.UpdateAsync(supplier);
        return errors;
    }

    public async Task<bool> ToggleStatusAsync(int supplierId)
    {
        var supplier = await repo.FindByIdAsync(supplierId);
        if (supplier is null) return false;

        supplier.Status = supplier.Status == "ACTIVE" ? "INACTIVE" : "ACTIVE";
        await repo.UpdateAsync(supplier);
        return true;
    }

    private static string? CleanString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
