using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize]
public sealed class MembersController(
    IMemberRepository memberRepository,
    ILogger<MembersController> logger) : Controller
{
    private const int MaxSearchLength = 100;

    [HttpGet]
    public async Task<IActionResult> Index(string? search, CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > MaxSearchLength)
        {
            return BadRequest($"Search cannot exceed {MaxSearchLength} characters.");
        }

        var model = new MemberListViewModel { Search = normalizedSearch };
        try
        {
            model = new MemberListViewModel
            {
                Search = normalizedSearch,
                Members = await memberRepository.GetAllAsync(
                    normalizedSearch,
                    UserDivision.GetDivisionId(User),
                    cancellationToken)
            };
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The member list could not be loaded because the database configuration is invalid.");
            model = new MemberListViewModel
            {
                Search = normalizedSearch,
                DatabaseError = exception.Message
            };
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The member list could not be loaded from MySQL.");
            model = new MemberListViewModel
            {
                Search = normalizedSearch,
                DatabaseError = "Members could not be loaded. Check the MySQL connection and members table."
            };
        }

        ViewData["Title"] = "Members";
        ViewData["Description"] = "Manage student members.";
        ViewData["ActiveSection"] = "members";
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Export(string? search, CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > MaxSearchLength)
        {
            return BadRequest("Search cannot exceed 100 characters.");
        }

        try
        {
            var members = await memberRepository.GetAllAsync(
                normalizedSearch,
                UserDivision.GetDivisionId(User),
                cancellationToken);
            return File(
                MemberWordExporter.Export(members),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "SOMAMS-Members.docx");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The members Word export could not be created because the database configuration is invalid.");
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The members Word export could not be loaded from MySQL.");
            TempData["ErrorMessage"] = "Members could not be exported. Check the MySQL connection and members table.";
        }

        return RedirectToAction(nameof(Index), new { search = normalizedSearch });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        string memberId,
        string? search,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(memberId) || memberId.Length > 20)
        {
            return BadRequest();
        }

        try
        {
            await memberRepository.DeleteAsync(memberId, UserDivision.GetDivisionId(User), cancellationToken);
            TempData["SuccessMessage"] = $"Member {memberId} was deleted.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member {MemberId} could not be deleted due to invalid database configuration.", memberId);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Member {MemberId} could not be deleted from MySQL.", memberId);
            TempData["ErrorMessage"] = "The member could not be deleted. Check the database connection and try again.";
        }

        return RedirectToAction(nameof(Index), new { search });
    }
}
