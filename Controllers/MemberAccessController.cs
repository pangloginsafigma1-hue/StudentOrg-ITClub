using System.Text;
using System.Text.RegularExpressions;
using System.Security.Claims;
using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[AllowAnonymous]
[EnableRateLimiting("member-access")]
public sealed class MemberAccessController(
    IMemberPortalRepository memberRepository,
    IDivisionRepository divisionRepository,
    IConfiguration configuration,
    ILogger<MemberAccessController> logger) : Controller
{
    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToSignedInArea();
        }

        return View(new MemberLoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(MemberLoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var member = await memberRepository.AuthenticateAsync(
                model.MemberId.Trim(),
                model.Password,
                cancellationToken);
            if (member is null)
            {
                ModelState.AddModelError(string.Empty, "The Student ID or password is incorrect, or the account is not active.");
                model.Password = string.Empty;
                return View(model);
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, member.MemberId),
                new(ClaimTypes.Name, member.StudentName),
                new(ClaimTypes.Role, "Member"),
                new("member_id", member.MemberId)
            };
            if (member.DivisionId is int divisionId)
            {
                claims.Add(new Claim("division_id", divisionId.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(member.DivisionName))
            {
                claims.Add(new Claim("division_name", member.DivisionName));
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    AllowRefresh = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });
            return RedirectToAction("Index", "MemberPortal");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member portal sign-in failed because the database configuration is invalid.");
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member portal sign-in could not be verified in MySQL.");
            ModelState.AddModelError(string.Empty, "Member sign-in is unavailable. Check the database connection and portal schema.");
        }

        model.Password = string.Empty;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Register(CancellationToken cancellationToken)
    {
        try
        {
            return View(new MemberRegistrationViewModel
            {
                Divisions = await divisionRepository.GetAllAsync(cancellationToken)
            });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member registration could not load divisions because the database configuration is invalid.");
            ViewData["DatabaseError"] = exception.Message;
            return View(new MemberRegistrationViewModel());
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member registration could not load divisions from MySQL.");
            ViewData["DatabaseError"] = "Registration is unavailable. Check the database connection and division setup.";
            return View(new MemberRegistrationViewModel());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(MemberRegistrationViewModel model, CancellationToken cancellationToken)
    {
        model.MemberId = model.MemberId?.Trim() ?? string.Empty;
        model.StudentName = model.StudentName?.Trim() ?? string.Empty;
        try
        {
            if (!GetStudentIdPattern().IsMatch(model.MemberId))
            {
                ModelState.AddModelError(nameof(model.MemberId),
                    $"Enter a valid Student ID. Use the format {configuration["StudentIdExample"] ?? "26-0001"}.");
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member registration could not validate the Student ID format.");
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        if (model.Password is null || Encoding.UTF8.GetByteCount(model.Password) > 72)
        {
            ModelState.AddModelError(nameof(model.Password), "The password must not exceed 72 UTF-8 bytes.");
        }
        if (!MemberLookups.Courses.Contains(model.Course, StringComparer.Ordinal)
            || !MemberLookups.YearLevels.Contains(model.YearLevel, StringComparer.Ordinal))
        {
            ModelState.AddModelError(string.Empty, "Choose a valid course and year level.");
        }

        IReadOnlyList<Division> divisions;
        try
        {
            divisions = await divisionRepository.GetAllAsync(cancellationToken);
            if (model.DivisionId is not int divisionId
                || !divisions.Any(division => division.DivisionId == divisionId))
            {
                ModelState.AddModelError(nameof(model.DivisionId), "Choose a valid division.");
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member registration validation could not load divisions.");
            ModelState.AddModelError(string.Empty, exception.Message);
            divisions = [];
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member registration validation could not load divisions from MySQL.");
            ModelState.AddModelError(string.Empty, "Registration is unavailable. Check the database connection and division setup.");
            divisions = [];
        }

        model = CopyWithDivisions(model, divisions);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            if (!await memberRepository.RegisterAsync(model, cancellationToken))
            {
                ModelState.AddModelError(nameof(model.MemberId),
                    "This Student ID already has a portal account or a request awaiting review.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Your registration request was submitted. You can sign in after an administrator approves it.";
            return RedirectToAction(nameof(Login));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member registration could not be saved because the database configuration is invalid.");
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member registration could not be saved to MySQL.");
            ModelState.AddModelError(string.Empty,
                "Registration could not be submitted. Check that the member portal database migration has been applied.");
        }

        return View(model);
    }

    private IActionResult RedirectToSignedInArea() =>
        User.IsInRole("Member")
            ? RedirectToAction("Index", "MemberPortal")
            : RedirectToAction("Index", "Dashboard");

    private static MemberRegistrationViewModel CopyWithDivisions(
        MemberRegistrationViewModel model,
        IReadOnlyList<Division> divisions) =>
        new()
        {
            MemberId = model.MemberId,
            StudentName = model.StudentName,
            Course = model.Course,
            YearLevel = model.YearLevel,
            DivisionId = model.DivisionId,
            Password = model.Password,
            ConfirmPassword = model.ConfirmPassword,
            Divisions = divisions
        };

    private Regex GetStudentIdPattern()
    {
        var pattern = configuration["StudentIdPattern"] ?? @"^\d{2}-\d{4}$";
        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "The StudentIdPattern setting is invalid. Check the configured student ID validation pattern.",
                exception);
        }
    }
}
