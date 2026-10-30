using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "SuperAdmin,DivisionAdmin")]
public sealed class MembersController(
    IMemberRepository memberRepository,
    IMemberPortalRepository memberPortalRepository,
    IEmailSender emailSender,
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
                    cancellationToken),
                PendingRegistrations = await memberPortalRepository.GetPendingRegistrationsAsync(
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

        ViewData["Title"] = "Membership";
        ViewData["Description"] = "Manage student members and review join requests.";
        ViewData["ActiveSection"] = "membership";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRegistration(string memberId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(memberId) || memberId.Length > 20)
        {
            return BadRequest();
        }

        try
        {
            var approval = await memberPortalRepository.ApproveRegistrationAsync(
                memberId,
                UserDivision.GetDivisionId(User),
                GetAdminId(),
                cancellationToken);
            if (!approval.Approved)
            {
                TempData["ErrorMessage"] = "This request is no longer pending or is outside your division.";
            }
            else if (string.IsNullOrWhiteSpace(approval.Email))
            {
                TempData["SuccessMessage"] = $"Registration for {memberId} was approved.";
                TempData["ErrorMessage"] = "No email address was recorded for this older request, so the member could not be notified.";
            }
            else
            {
                try
                {
                    await emailSender.SendRegistrationApprovedAsync(
                        approval.Email,
                        approval.StudentName,
                        memberId,
                        CancellationToken.None);
                    TempData["SuccessMessage"] = $"Registration for {memberId} was approved and an email notification was sent.";
                }
                catch (InvalidOperationException exception)
                {
                    logger.LogError(exception, "Registration for member {MemberId} was approved, but its email notification could not be sent.", memberId);
                    TempData["SuccessMessage"] = $"Registration for {memberId} was approved.";
                    TempData["ErrorMessage"] = "The member notification email could not be sent. Check the email configuration and contact the member.";
                }
                catch (System.Net.Mail.SmtpException exception)
                {
                    logger.LogError(exception, "Registration for member {MemberId} was approved, but its email notification could not be sent.", memberId);
                    TempData["SuccessMessage"] = $"Registration for {memberId} was approved.";
                    TempData["ErrorMessage"] = "The member notification email could not be sent. Check the email configuration and contact the member.";
                }
                catch (Exception exception) when (exception is FormatException or ArgumentException)
                {
                    logger.LogError(exception, "Registration for member {MemberId} was approved, but its email address or sender configuration is invalid.", memberId);
                    TempData["SuccessMessage"] = $"Registration for {memberId} was approved.";
                    TempData["ErrorMessage"] = "The member notification email could not be sent because an email address is invalid. Check the email configuration and contact the member.";
                }
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Registration for member {MemberId} could not be approved.", memberId);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Registration for member {MemberId} could not be approved in MySQL.", memberId);
            TempData["ErrorMessage"] = "The registration could not be approved. Check the member portal schema and existing member record.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRegistration(string memberId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(memberId) || memberId.Length > 20)
        {
            return BadRequest();
        }

        try
        {
            var rejected = await memberPortalRepository.RejectRegistrationAsync(
                memberId,
                UserDivision.GetDivisionId(User),
                GetAdminId(),
                cancellationToken);
            TempData[rejected ? "SuccessMessage" : "ErrorMessage"] = rejected
                ? $"Registration for {memberId} was rejected."
                : "This request is no longer pending or is outside your division.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogWarning(exception, "Registration for member {MemberId} could not be rejected.", memberId);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Registration for member {MemberId} could not be rejected in MySQL.", memberId);
            TempData["ErrorMessage"] = "The registration could not be rejected. Check the member portal schema.";
        }

        return RedirectToAction(nameof(Index));
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

    private int GetAdminId() =>
        int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var adminId)
            ? adminId
            : throw new InvalidOperationException("The signed-in administrator account is invalid.");
}
