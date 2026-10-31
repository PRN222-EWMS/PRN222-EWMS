using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.Services.Admin;
using PRN222_EWMS_Web.Services.Account;
using PRN222_EWMS_Web.Services.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Services.AddControllersWithViews(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.ResponseCacheAttribute { NoStore = true, Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.None }));
builder.Services.AddDbContext<EwmsDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DBDefault") ?? builder.Configuration.GetConnectionString("MyCnn")));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Sales.ISalesRepository, PRN222_EWMS_Web.Repositories.Sales.SalesRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Sales.ISalesService, PRN222_EWMS_Web.Services.Sales.SalesService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Purchasing.IPurchaseOrderRepository, PRN222_EWMS_Web.Repositories.Purchasing.PurchaseOrderRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Purchasing.IPurchaseOrderService, PRN222_EWMS_Web.Services.Purchasing.PurchaseOrderService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Authentication.IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AccountCookieEvents>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Authentication/Login";
    options.AccessDeniedPath = "/Authentication/AccessDenied";
    options.Cookie.Name = "EWMS.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.EventsType = typeof(AccountCookieEvents);
});
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
await AdminBootstrap.InitializeAsync(app.Services, app.Configuration);
app.Run();
