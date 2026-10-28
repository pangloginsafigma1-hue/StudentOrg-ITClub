using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<MySqlConnectionFactory>();
builder.Services.AddScoped<IAdminAuthService, AdminAuthService>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<IActivityRepository, ActivityRepository>();
builder.Services.AddScoped<IAttendanceRepository, AttendanceRepository>();
builder.Services.AddScoped<IReportsRepository, ReportsRepository>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IDivisionRepository, DivisionRepository>();
builder.Services.AddScoped<IAdminAccountRepository, AdminAccountRepository>();
builder.Services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".SOMAMS.Auth.v2";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.LoginPath = "/Auth/Login";
        options.AccessDeniedPath = "/Auth/Login";
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
            var validClaims = hasAdminId
                && (role == "SuperAdmin" && divisionClaim is null
                    || role == "DivisionAdmin" && hasDivisionId && parsedDivisionId > 0);
            int? divisionId = role == "DivisionAdmin" && hasDivisionId ? parsedDivisionId : null;

            var isCurrent = validClaims
                && await context.HttpContext.RequestServices
                    .GetRequiredService<IAdminAuthService>()
                    .IsAuthorizationCurrentAsync(
                        parsedAdminId,
                        role!,
                        divisionId,
                        context.HttpContext.RequestAborted);

            if (!isCurrent)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
