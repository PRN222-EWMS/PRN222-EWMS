using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.OAuth;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PRN222_EWMS_Web.Models;
using PRN222_EWMS_Web.Repositories.User;
using PRN222_EWMS_Web.Repositories.Warehouse;
using PRN222_EWMS_Web.Services.Admin;
using PRN222_EWMS_Web.Repositories.Admin;
using PRN222_EWMS_Web.Services.Account;
using PRN222_EWMS_Web.Services.Authentication;
using PRN222_EWMS_Web.Repositories.Supplier;
using PRN222_EWMS_Web.Services.Supplier;
using PRN222_EWMS_Web.Services.Warehouse;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Services.AddControllersWithViews(options => options.Filters.Add(new Microsoft.AspNetCore.Mvc.ResponseCacheAttribute { NoStore = true, Location = Microsoft.AspNetCore.Mvc.ResponseCacheLocation.None }));
builder.Services.AddDbContext<EwmsDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DBDefault") ?? builder.Configuration.GetConnectionString("MyCnn")));
builder.Services.AddScoped<PRN222_EWMS_Web.Services.DocumentCodeGenerator>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Sales.ISalesRepository, PRN222_EWMS_Web.Repositories.Sales.SalesRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Sales.ICustomerReturnOrderRepository, PRN222_EWMS_Web.Repositories.Sales.CustomerReturnOrderRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Sales.ISalesService, PRN222_EWMS_Web.Services.Sales.SalesService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Sales.ICustomerReturnOrderService, PRN222_EWMS_Web.Services.Sales.CustomerReturnOrderService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Purchasing.IPurchaseOrderRepository, PRN222_EWMS_Web.Repositories.Purchasing.PurchaseOrderRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Purchasing.IWarehouseCapacityEngine, PRN222_EWMS_Web.Services.Purchasing.WarehouseCapacityEngine>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Purchasing.IPurchaseOrderService, PRN222_EWMS_Web.Services.Purchasing.PurchaseOrderService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Purchasing.IPurchasingDashboardService, PRN222_EWMS_Web.Services.Purchasing.PurchasingDashboardService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.WarehouseStaff.IWarehouseStaffRepository, PRN222_EWMS_Web.Repositories.WarehouseStaff.WarehouseStaffRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.WarehouseStaff.IWarehouseStaffService, PRN222_EWMS_Web.Services.WarehouseStaff.WarehouseStaffService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.WarehouseStaff.IInboundRepository, PRN222_EWMS_Web.Repositories.WarehouseStaff.InboundRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.WarehouseStaff.IInboundService, PRN222_EWMS_Web.Services.WarehouseStaff.InboundService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.WarehouseStaff.IReturnReceiveRepository, PRN222_EWMS_Web.Repositories.WarehouseStaff.ReturnReceiveRepository>();
builder.Services.AddScoped<
    PRN222_EWMS_Web.Repositories.WarehouseStaff.WarehouseTransferRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.WarehouseStaff.TransferDispatchWorkService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.WarehouseStaff.TransferReceiptWorkService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.WarehouseStaff.IReturnReceiveService, PRN222_EWMS_Web.Services.WarehouseStaff.ReturnReceiveService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.WarehouseStaff.IPutawayRepository, PRN222_EWMS_Web.Repositories.WarehouseStaff.PutawayRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.WarehouseStaff.IPutawayService, PRN222_EWMS_Web.Services.WarehouseStaff.PutawayService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Warehouse.IOutboundService, PRN222_EWMS_Web.Services.Warehouse.OutboundService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Warehouse.IOutboundRepository, PRN222_EWMS_Web.Repositories.Warehouse.OutboundRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Warehouse.IOutboundSupplyService, PRN222_EWMS_Web.Services.Warehouse.OutboundSupplyService>();
builder.Services.AddScoped<ISourceReservationService, SourceReservationService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Sales.SalesOrderSourceService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Warehouse.ITransferOrderService, PRN222_EWMS_Web.Services.Warehouse.TransferOrderService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TransferStockService>();
builder.Services.AddScoped<TransferCapacityService>();
builder.Services.AddScoped<TransferExpiryService>();
builder.Services.AddHostedService<TransferExpiryWorker>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Ceo.ICeoPurchaseProgressRepository, PRN222_EWMS_Web.Repositories.Ceo.CeoPurchaseProgressRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Ceo.ICeoPurchaseProgressService, PRN222_EWMS_Web.Services.Ceo.CeoPurchaseProgressService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Ceo.ICeoDashboardRepository, PRN222_EWMS_Web.Repositories.Ceo.CeoDashboardRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Ceo.ICeoDashboardService, PRN222_EWMS_Web.Services.Ceo.CeoDashboardService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Ceo.ICeoStocktakeAdjustmentService, PRN222_EWMS_Web.Services.Ceo.CeoStocktakeAdjustmentService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Manager.IManagerInboundRepository, PRN222_EWMS_Web.Repositories.Manager.ManagerInboundRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Manager.IManagerInboundService, PRN222_EWMS_Web.Services.Manager.ManagerInboundService>();
builder.Services.AddScoped<
    PRN222_EWMS_Web.Repositories.Manager.IManagerTransferRepository,
    PRN222_EWMS_Web.Repositories.Manager.ManagerTransferRepository>();

builder.Services.AddScoped<
    PRN222_EWMS_Web.Services.Manager.IManagerTransferService,
    PRN222_EWMS_Web.Services.Manager.ManagerTransferService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Manager.IManagerStocktakeRepository, PRN222_EWMS_Web.Repositories.Manager.ManagerStocktakeRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Manager.IManagerStocktakeService, PRN222_EWMS_Web.Services.Manager.ManagerStocktakeService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Stocktaking.IStocktakeRepository, PRN222_EWMS_Web.Repositories.Stocktaking.StocktakeRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Stocktaking.IStocktakeExecutionService, PRN222_EWMS_Web.Services.Stocktaking.StocktakeExecutionService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Stocktaking.IStocktakeReviewService, PRN222_EWMS_Web.Services.Stocktaking.StocktakeReviewService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Stocktaking.IStocktakeHandlingService, PRN222_EWMS_Web.Services.Stocktaking.StocktakeHandlingService>();
builder.Services.AddScoped<IWarehouseRepository, WarehouseRepository>();
builder.Services.AddScoped<IWarehouseService, WarehouseService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Authentication.IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<SmtpEmailSender>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AccountCookieEvents>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<PRN222_EWMS_Web.Repositories.Customers.ICustomerRepository, PRN222_EWMS_Web.Repositories.Customers.CustomerRepository>();
builder.Services.AddScoped<PRN222_EWMS_Web.Services.Customers.ICustomerService, PRN222_EWMS_Web.Services.Customers.CustomerService>();
var authenticationBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
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
authenticationBuilder.AddCookie("GoogleExternal", options =>
{
    options.Cookie.Name = "EWMS.GoogleExternal";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
});
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authenticationBuilder.AddOAuth("Google", options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = "GoogleExternal";
        options.CallbackPath = "/signin-google";
        options.UsePkce = true;
        options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        options.TokenEndpoint = "https://oauth2.googleapis.com/token";
        options.UserInformationEndpoint = "https://openidconnect.googleapis.com/v1/userinfo";
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.Events = new OAuthEvents
        {
            OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect("/Authentication/Login?googleError=1");
                return Task.CompletedTask;
            },
            OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                var profile = document.RootElement;
                if (!profile.TryGetProperty("sub", out var sub) || string.IsNullOrWhiteSpace(sub.GetString())
                    || !profile.TryGetProperty("email", out var email) || string.IsNullOrWhiteSpace(email.GetString())
                    || !profile.TryGetProperty("email_verified", out var verified) || verified.ValueKind != JsonValueKind.True)
                    throw new InvalidOperationException("Google did not provide a verified account identifier.");
                context.Identity!.AddClaim(new System.Security.Claims.Claim("google:sub", sub.GetString()!));
                context.Identity.AddClaim(new System.Security.Claims.Claim("google:email", email.GetString()!));
                if (profile.TryGetProperty("hd", out var hostedDomain) && !string.IsNullOrWhiteSpace(hostedDomain.GetString()))
                    context.Identity.AddClaim(new System.Security.Claims.Claim("google:hd", hostedDomain.GetString()!));
            }
        };
    });
}
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddScoped<
    PRN222_EWMS_Web.Repositories.Products.IProductRepository,
    PRN222_EWMS_Web.Repositories.Products.ProductRepository>();

builder.Services.AddScoped<
    PRN222_EWMS_Web.Services.Products.IProductService,
    PRN222_EWMS_Web.Services.Products.ProductService>();

builder.Services.AddScoped<
    PRN222_EWMS_Web.Repositories.Manager.IManagerDashboardRepository,
    PRN222_EWMS_Web.Repositories.Manager.ManagerDashboardRepository>();

builder.Services.AddScoped<
    PRN222_EWMS_Web.Services.Manager.IManagerDashboardService,
    PRN222_EWMS_Web.Services.Manager.ManagerDashboardService>();

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
