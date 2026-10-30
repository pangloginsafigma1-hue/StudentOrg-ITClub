using System.Security.Claims;
using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "SuperAdmin,DivisionAdmin")]
public sealed class SettingsController(
    ISettingsRepository settingsRepository,
    ILogger<SettingsController> logger) : Controller
{
    private const int MaxPhotoBytes = 4 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Challenge();
        }

        try
        {
            var profile = await settingsRepository.GetProfileAsync(adminId, cancellationToken);
            if (profile is null)
            {
                return NotFound();
            }

            ViewData["Title"] = "Profile";
            ViewData["Description"] = "Manage your administrator profile and password.";
            ViewData["ActiveSection"] = "settings";
            return View(profile);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator settings could not be loaded because the database configuration is invalid.");
            SetPageMetadata();
            return View(new SettingsViewModel { DatabaseError = exception.Message });
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator settings could not be loaded from MySQL.");
            SetPageMetadata();
            return View(new SettingsViewModel
            {
                DatabaseError = "Profile could not be loaded from MySQL. Check the database connection and schema."
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Photo(CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Challenge();
        }

        try
        {
            var photo = await settingsRepository.GetPhotoAsync(adminId, cancellationToken);
            if (photo is null || !TryGetImageContentType(photo, out var contentType))
            {
                return NotFound();
            }

            return File(photo, contentType);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator photo could not be loaded because the database configuration is invalid.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator photo could not be loaded from MySQL.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxPhotoBytes + 128 * 1024)]
    public async Task<IActionResult> Index(SettingsViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetAdminId(out var adminId))
        {
            return Challenge();
        }

        model.FullName = model.FullName.Trim();
        model.Username = model.Username.Trim();
        model.Email = model.Email.Trim();

        byte[]? photo = null;
        if (model.Photo is { Length: > 0 } upload)
        {
            if (upload.Length > MaxPhotoBytes)
            {
                ModelState.AddModelError(nameof(model.Photo), "Choose an image smaller than 4 MB.");
            }
            else
            {
                await using var stream = new MemoryStream((int)upload.Length);
                await upload.CopyToAsync(stream, cancellationToken);
                photo = stream.ToArray();
                if (!TryGetImageContentType(photo, out _))
                {
                    ModelState.AddModelError(nameof(model.Photo), "Choose a valid JPG or PNG image.");
                }
            }
        }

        if (!ModelState.IsValid)
        {
            SetPageMetadata();
            return View(model);
        }

        try
        {
            if (await settingsRepository.IsUsernameTakenAsync(model.Username, adminId, cancellationToken))
            {
                ModelState.AddModelError(nameof(model.Username), "That username is already taken.");
                SetPageMetadata();
                return View(model);
            }

            await settingsRepository.UpdateProfileAsync(adminId, model, photo, cancellationToken);
            await RefreshSignInAsync(model.FullName, model.Username);
            TempData["SuccessMessage"] = "Your profile changes were saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (MySqlException exception) when (exception.Number == 1062)
        {
            ModelState.AddModelError(nameof(model.Username), "That username is already taken.");
            SetPageMetadata();
            return View(model);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Administrator settings could not be saved due to invalid database configuration.");
            ModelState.AddModelError(string.Empty, exception.Message);
            model.DatabaseError = exception.Message;
            SetPageMetadata();
            return View(model);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Administrator {AdminId} settings could not be saved.", adminId);
            model.DatabaseError = "Your changes could not be saved. Check the database connection and try again.";
            SetPageMetadata();
            return View(model);
        }
    }

    private void SetPageMetadata()
    {
        ViewData["Title"] = "Profile";
        ViewData["Description"] = "Manage your administrator profile and password.";
        ViewData["ActiveSection"] = "settings";
    }

    private bool TryGetAdminId(out int adminId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out adminId);

    private async Task RefreshSignInAsync(string fullName, string username)
    {
        var authentication = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var claims = User.Claims
            .Where(claim => claim.Type is not ClaimTypes.Name and not "username")
            .Append(new Claim(ClaimTypes.Name, fullName))
            .Append(new Claim("username", username));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            authentication.Properties ?? new AuthenticationProperties());
    }

    private static bool TryGetImageContentType(byte[] photo, out string contentType)
    {
        if (photo.Length >= 8
            && photo.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            contentType = "image/png";
            return true;
        }

        if (photo.Length >= 3 && photo[0] == 0xFF && photo[1] == 0xD8 && photo[2] == 0xFF)
        {
            contentType = "image/jpeg";
            return true;
        }

        contentType = string.Empty;
        return false;
    }
}
