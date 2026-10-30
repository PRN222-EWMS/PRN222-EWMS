using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.ViewModels.Admin;

namespace PRN222_EWMS_Web.Services.Admin;

public interface IUserService
{
    Task<List<User>> GetAllAsync();
    Task<UserListViewModel> GetAccountListAsync(UserListFilter filter);
    Task PopulateOptionsAsync(CreateAccountViewModel model);
    Task<Dictionary<string, string>> CreateAsync(CreateAccountViewModel model);
}
