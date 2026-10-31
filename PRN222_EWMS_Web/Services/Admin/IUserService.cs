using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Services.Admin;

public interface IUserService
{
    Task<List<User>> GetAllAsync();
    Task<UserListViewModel> GetAccountListAsync(UserListFilter filter);
    Task<AccountDetailsViewModel?> GetAccountDetailsAsync(int userId);
    Task<EditAccountViewModel?> GetEditAccountAsync(int userId, UserListFilter? returnFilter = null);
    Task<bool> PopulateEditOptionsAsync(EditAccountViewModel model, int userId);
    Task<Dictionary<string, string>> UpdateAccountAsync(int userId, EditAccountViewModel model);
    Task PopulateOptionsAsync(CreateAccountViewModel model);
    Task<Dictionary<string, string>> CreateAsync(CreateAccountViewModel model);
}
