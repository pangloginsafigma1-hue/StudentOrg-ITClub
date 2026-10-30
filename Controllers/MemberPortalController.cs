using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "Member")]
public sealed class MemberPortalController(
    IMemberPortalRepository memberRepository,
    ILogger<MemberPortalController> logger) : Controller
{
    [HttpGet]
    public Task<IActionResult> Index(CancellationToken cancellationToken) =>
        RenderPortalPageAsync("Index", "Home", "Your membership at a glance.", cancellationToken);

    [HttpGet]
    public Task<IActionResult> Activities(CancellationToken cancellationToken) =>
        RenderPortalPageAsync("Activities", "Activities", "Upcoming activities for you.", cancellationToken);

    [HttpGet]
    public Task<IActionResult> Record(CancellationToken cancellationToken) =>
        RenderAccountPageAsync(new MemberAccountViewModel(), cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        MemberAccountViewModel model,
        CancellationToken cancellationToken)
    {
        var currentPassword = model.CurrentPassword;
        var newPassword = model.NewPassword;
        if (currentPassword is null || newPassword is null
            || newPassword.Length < 8
            || System.Text.Encoding.UTF8.GetByteCount(currentPassword) > 72
            || System.Text.Encoding.UTF8.GetByteCount(newPassword) > 72)
        {
            ModelState.AddModelError(nameof(model.NewPassword), "Use a password between 8 and 72 UTF-8 bytes.");
        }

        if (!ModelState.IsValid || currentPassword is null || newPassword is null)
        {
            model.CurrentPassword = string.Empty;
            model.NewPassword = string.Empty;
            model.ConfirmPassword = string.Empty;
            return await RenderAccountPageAsync(model, cancellationToken);
        }

        try
        {
            var changed = await memberRepository.ChangePortalPasswordAsync(
                GetMemberId(),
                currentPassword,
                newPassword,
                cancellationToken);
            if (!changed)
            {
                TempData["ErrorMessage"] = "The current password is incorrect or the member account is unavailable.";
            }
            else
            {
                TempData["SuccessMessage"] = "Your password was changed.";
            }

            return RedirectToAction(nameof(Record));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member password could not be changed.");
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(Record));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member password could not be changed in MySQL.");
            TempData["ErrorMessage"] = "Your password could not be changed. Check the member portal database migration.";
            return RedirectToAction(nameof(Record));
        }
    }

    [HttpGet]
    public Task<IActionResult> Attendance(CancellationToken cancellationToken) =>
        RenderPortalPageAsync("Attendance", "My Attendance", "Your recorded activity participation.", cancellationToken);

    [HttpGet]
    public async Task<IActionResult> Updates(CancellationToken cancellationToken)
    {
        try
        {
            var portal = await memberRepository.GetPortalAsync(GetMemberId(), cancellationToken);
            SetPageMetadata("Updates", "Organization and division announcements.");
            return View(portal);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member announcements could not be loaded.");
            return ErrorPage("Updates", exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member announcements could not be loaded from MySQL.");
            return ErrorPage("Updates", "Updates could not be loaded. Check the member portal database migration.");
        }
    }

    private async Task<IActionResult> RenderPortalPageAsync(
        string viewName,
        string title,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            var model = await memberRepository.GetPortalAsync(GetMemberId(), cancellationToken);
            SetPageMetadata(title, description);
            return View(viewName, model);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The member portal page {Page} could not be loaded.", viewName);
            return ErrorPage(title, exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The member portal page {Page} could not be loaded from MySQL.", viewName);
            return ErrorPage(title, "Member information could not be loaded. Check the member portal database migration.");
        }
    }

    private async Task<IActionResult> RenderAccountPageAsync(
        MemberAccountViewModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            var portal = await memberRepository.GetPortalAsync(GetMemberId(), cancellationToken);
            model.Member = portal.Member;
            SetPageMetadata("Account", "Your membership information and password.");
            return View("Record", model);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The member account page could not be loaded.");
            return ErrorPage("Account", exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The member account page could not be loaded from MySQL.");
            return ErrorPage("Account", "Member information could not be loaded. Check the member portal database migration.");
        }
    }

    private IActionResult ErrorPage(string title, string message)
    {
        SetPageMetadata(title, "Member portal");
        ViewData["DatabaseError"] = message;
        return View("Error");
    }

    private string GetMemberId() =>
        User.FindFirst("member_id")?.Value
        ?? throw new InvalidOperationException("The signed-in account does not contain a member ID.");

    private void SetPageMetadata(string title, string description)
    {
        ViewData["Title"] = title;
        ViewData["Description"] = description;
        ViewData["ActiveSection"] = title.ToLowerInvariant().Replace(' ', '-');
    }
}
