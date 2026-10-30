using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("member-access", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<IMemberPortalRepository, MemberPortalRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IReportsRepository, ReportsRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IDivisionRepository, DivisionRepository>();
builder.Services.AddScoped<IAdminAccountRepository, AdminAccountRepository>();
builder.Services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IMemberPasswordResetService, MemberPasswordResetService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".SOMAMS.Auth.v2";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnValidatePrincipal = async context =>
        {
            var principal = context.Principal;
            var adminId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var role = principal?.FindFirstValue(ClaimTypes.Role);
            var divisionClaim = principal?.FindFirstValue("division_id");
            var hasAdminId = int.TryParse(adminId, out var parsedAdminId);
            var hasDivisionId = int.TryParse(divisionClaim, out var parsedDivisionId);
            var isCurrent = false;
            if (role == "Member")
            {
                var memberId = principal?.FindFirstValue("member_id");
                isCurrent = !string.IsNullOrWhiteSpace(memberId)
                    && string.Equals(adminId, memberId, StringComparison.Ordinal)
                    && await context.HttpContext.RequestServices
                        .GetRequiredService<IMemberPortalRepository>()
                        .IsAuthorizationCurrentAsync(memberId, context.HttpContext.RequestAborted);
            }
            else
            {
                var validClaims = hasAdminId
                    && (role == "SuperAdmin" && divisionClaim is null
                        || role == "DivisionAdmin" && hasDivisionId && parsedDivisionId > 0);
                int? divisionId = role == "DivisionAdmin" && hasDivisionId ? parsedDivisionId : null;
                isCurrent = validClaims
                    && await context.HttpContext.RequestServices
                        .GetRequiredService<IAdminAuthService>()
                        .IsAuthorizationCurrentAsync(
                            parsedAdminId,
                            role!,
                            divisionId,
                            context.HttpContext.RequestAborted);
            }

            if (!isCurrent)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
        options.Events.OnRedirectToLogin = context =>
        {
            var loginPath = context.Request.Path.StartsWithSegments("/MemberPortal")
                ? "/MemberAccess/Login"
                : "/Auth/Login";
            context.Response.Redirect(loginPath);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
