using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "SuperAdmin,DivisionAdmin")]
public sealed class AnnouncementsController(
    IMemberPortalRepository memberRepository,
    IDivisionRepository divisionRepository,
    ILogger<AnnouncementsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SetPageMetadata();
        try
        {
            return View(await memberRepository.GetAnnouncementsAsync(
                UserDivision.GetDivisionId(User),
                User.IsInRole("SuperAdmin"),
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Announcements could not be loaded.");
            ViewData["DatabaseError"] = exception.Message;
            return View(Array.Empty<MemberAnnouncement>());
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Announcements could not be loaded from MySQL.");
            ViewData["DatabaseError"] = "Announcements could not be loaded. Apply the member portal database migration.";
            return View(Array.Empty<MemberAnnouncement>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        try
        {
            SetPageMetadata();
            ViewData["Title"] = "Create a post";
            return View(new AnnouncementFormViewModel
            {
                Divisions = User.IsInRole("SuperAdmin")
                    ? await divisionRepository.GetAllAsync(cancellationToken)
                    : []
            });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Announcement form could not load divisions.");
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Announcement form could not load divisions from MySQL.");
            TempData["ErrorMessage"] = "The announcement form could not be loaded. Check the database connection.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AnnouncementFormViewModel model, CancellationToken cancellationToken)
    {
        var divisionScope = UserDivision.GetDivisionId(User);
        if (divisionScope is int assignedDivisionId)
        {
            model.DivisionIds = [assignedDivisionId];
            ModelState.Remove(nameof(model.DivisionIds));
        }
        else
        {
            model.DivisionIds = model.DivisionIds
                .Where(id => id > 0)
                .Distinct()
                .ToArray();
            if (model.DivisionIds.Length > 2)
            {
                ModelState.AddModelError(nameof(model.DivisionIds), "Choose no more than two divisions.");
            }
        }

        IReadOnlyList<Division> divisions;
        try
        {
            divisions = User.IsInRole("SuperAdmin")
                ? await divisionRepository.GetAllAsync(cancellationToken)
                : [];
            if (model.DivisionIds.Any(requestedId =>
                !divisions.Any(division => division.DivisionId == requestedId)))
            {
                ModelState.AddModelError(nameof(model.DivisionIds), "Choose valid divisions.");
            }
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Announcement division validation failed.");
            ModelState.AddModelError(string.Empty, exception.Message);
            divisions = [];
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Announcement divisions could not be loaded.");
            ModelState.AddModelError(string.Empty, "Division options could not be loaded.");
            divisions = [];
        }

        model = new AnnouncementFormViewModel
        {
            Title = model.Title?.Trim() ?? string.Empty,
            Body = model.Body?.Trim() ?? string.Empty,
            DivisionIds = model.DivisionIds,
            Divisions = divisions
        };
        if (!ModelState.IsValid)
        {
            SetPageMetadata();
            ViewData["Title"] = "Create a post";
            return View(model);
        }

        try
        {
            if (!int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var adminId))
            {
                throw new InvalidOperationException("The signed-in administrator account is invalid.");
            }
            await memberRepository.CreateAnnouncementAsync(
                model.Title,
                model.Body,
                model.DivisionIds,
                adminId,
                cancellationToken);
            TempData["SuccessMessage"] = "Your post was published.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Announcement could not be published.");
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Announcement could not be published to MySQL.");
            ModelState.AddModelError(string.Empty, "The post could not be published. Check the member portal database migration.");
        }

        model = new AnnouncementFormViewModel
        {
            Title = model.Title,
            Body = model.Body,
            DivisionIds = model.DivisionIds,
            Divisions = divisions
        };
        SetPageMetadata();
        ViewData["Title"] = "Create a post";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int announcementId, CancellationToken cancellationToken)
    {
        if (announcementId <= 0)
        {
            return BadRequest();
        }

        try
        {
            var deleted = await memberRepository.DeleteAnnouncementAsync(
                announcementId,
                UserDivision.GetDivisionId(User) is int scopedDivision
                    ? [scopedDivision]
                    : [],
                User.IsInRole("SuperAdmin"),
                cancellationToken);
            TempData[deleted ? "SuccessMessage" : "ErrorMessage"] = deleted
                ? "The announcement was deleted."
                : "This announcement no longer exists or is outside your division.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Announcement {AnnouncementId} could not be deleted.", announcementId);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Announcement {AnnouncementId} could not be deleted from MySQL.", announcementId);
            TempData["ErrorMessage"] = "The announcement could not be deleted. Check the member portal database migration.";
        }

        return RedirectToAction(nameof(Index));
    }

    private void SetPageMetadata()
    {
        ViewData["Title"] = "Announcements";
        ViewData["Description"] = "Create an announcement for your division or all members.";
        ViewData["ActiveSection"] = "announcements";
    }
}
