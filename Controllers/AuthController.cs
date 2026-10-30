using System.Security.Claims;
using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[AllowAnonymous]
public sealed class AuthController(
    IAdminAuthService authService,
    ILogger<AuthController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Login(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return User.IsInRole("Member")
                ? RedirectToAction("Index", "MemberPortal")
                : RedirectToAction("Index", "Dashboard");
        }

        try
        {
            var hasAdmin = await authService.PrepareDatabaseAsync(cancellationToken);
            if (!hasAdmin)
            {
                ViewData["DatabaseError"] =
                    "No administrator account exists yet. Configure BootstrapAdmin__Username, __FullName, __Email, and __Password to create one.";
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "SOMAMS database configuration is invalid.");
            ViewData["DatabaseError"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogWarning(exception, "SOMAMS database connection or schema check failed.");
            ViewData["DatabaseError"] = GetDatabaseErrorMessage(exception);
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AdminAccount? admin;
        try
        {
            admin = await authService.AuthenticateAsync(model.Username.Trim(), model.Password, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "SOMAMS database configuration is invalid.");
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "SOMAMS could not validate administrator credentials.");
            ModelState.AddModelError(string.Empty, GetDatabaseErrorMessage(exception));
            return View(model);
        }

        if (admin is null)
        {
            ModelState.AddModelError(string.Empty, "Incorrect username or password.");
            model.Password = string.Empty;
            return View(model);
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, admin.AdminId.ToString()),
            new Claim(ClaimTypes.Name, admin.FullName),
            new Claim(ClaimTypes.Role, admin.Role),
            new Claim("username", admin.Username)
        };
        var claimList = claims.ToList();
        if (admin.DivisionId is int divisionId)
        {
            claimList.Add(new Claim("division_id", divisionId.ToString()));
            claimList.Add(new Claim("division_name", admin.DivisionName!));
        }
        var identity = new ClaimsIdentity(claimList, CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            AllowRefresh = true,
            ExpiresUtc = DateTimeOffset.UtcNow.Add(model.RememberMe ? TimeSpan.FromDays(14) : TimeSpan.FromHours(8))
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var isMember = User.IsInRole("Member");
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return isMember
            ? RedirectToAction("Login", "MemberAccess")
            : RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return RedirectToAction("Index", "ForgotPassword");
    }

    private static string GetDatabaseErrorMessage(MySqlException exception) =>
        exception.Number is 1054 or 1146
            ? "ITClubOrgDB is missing required account or division schema. Run Database/04_divisions.sql against ITClubOrgDB, then try signing in again."
            : $"SOMAMS cannot verify your sign-in (MySQL error {exception.Number}). Check that ConnectionStrings__SOMAMS points to ITClubOrgDB, MySQL is running, and the database user has access.";
}
