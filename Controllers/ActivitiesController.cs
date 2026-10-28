using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize]
public sealed class ActivitiesController(
    IActivityRepository activityRepository,
    IDivisionRepository divisionRepository,
    ILogger<ActivitiesController> logger) : Controller
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

        var divisionId = UserDivision.GetDivisionId(User);
        ActivityListViewModel model;
        try
        {
            model = await BuildPageAsync(normalizedSearch, divisionId, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The activities list could not be loaded due to invalid database configuration.");
            model = new ActivityListViewModel { Search = normalizedSearch, DatabaseError = exception.Message };
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The activities list could not be loaded from MySQL.");
            model = new ActivityListViewModel
            {
                Search = normalizedSearch,
                DatabaseError = "Activities could not be loaded. Check the database connection and activities table."
            };
        }

        SetPageMetadata();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ActivityFormViewModel form, string? search, CancellationToken cancellationToken)
    {
        var normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > MaxSearchLength)
        {
            return BadRequest($"Search cannot exceed {MaxSearchLength} characters.");
        }

        var divisionScope = UserDivision.GetDivisionId(User);
        if (divisionScope is int assignedDivisionId)
        {
            form.DivisionId = assignedDivisionId;
            ModelState.Remove(nameof(form.DivisionId));
            ModelState.Remove("Form.DivisionId");
        }

        if (!MemberLookups.ActivityStatuses.Contains(form.Status, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(form.Status), "Choose a valid activity status.");
        }

        var isEdit = form.ActivityId > 0;
        if (form.ActivityId < 0)
        {
            return BadRequest();
        }

        IReadOnlyList<Division> divisions;
        try
        {
            divisions = await divisionRepository.GetAllAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Division options could not be loaded due to invalid database configuration.");
            ModelState.AddModelError(string.Empty, exception.Message);
            divisions = [];
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Division options could not be loaded from MySQL.");
            ModelState.AddModelError(string.Empty, "Division options could not be loaded. Check the database connection and divisions table.");
            divisions = [];
        }

        if (form.DivisionId is not int selectedDivisionId)
        {
            ModelState.AddModelError(nameof(form.DivisionId), "Choose a division.");
        }
        else if (!divisions.Any(division => division.DivisionId == selectedDivisionId))
        {
            ModelState.AddModelError(nameof(form.DivisionId), "Choose a valid division.");
        }

        if (!ModelState.IsValid)
        {
            return await ReturnFormWithErrorsAsync(form, normalizedSearch, divisionScope, cancellationToken);
        }

        var activity = new Activity
        {
            ActivityId = form.ActivityId,
            ActivityName = form.ActivityName.Trim(),
            Location = form.Location.Trim(),
            ActivityDate = form.ActivityDate.Date,
            Status = form.Status,
            Description = form.Description.Trim(),
            DivisionId = form.DivisionId
        };

        try
        {
            if (isEdit && await activityRepository.GetByIdAsync(activity.ActivityId, divisionScope, cancellationToken) is null)
            {
                return NotFound();
            }

            await activityRepository.SaveAsync(activity, divisionScope, cancellationToken);
            TempData["SuccessMessage"] = isEdit ? "Activity updated." : "Activity added.";
            return RedirectToAction(nameof(Index), new { search = normalizedSearch });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Activity {ActivityId} could not be saved due to invalid database configuration.", activity.ActivityId);
            ModelState.AddModelError(string.Empty, exception.Message);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Activity {ActivityId} could not be saved to MySQL.", activity.ActivityId);
            ModelState.AddModelError(string.Empty, "The activity could not be saved. Check the database connection and try again.");
        }

        return await ReturnFormWithErrorsAsync(form, normalizedSearch, divisionScope, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int activityId, string? search, CancellationToken cancellationToken)
    {
        if (activityId <= 0)
        {
            return BadRequest();
        }

        var divisionId = UserDivision.GetDivisionId(User);
        try
        {
            await activityRepository.DeleteAsync(activityId, divisionId, cancellationToken);
            TempData["SuccessMessage"] = "Activity deleted.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Activity {ActivityId} could not be deleted due to invalid database configuration.", activityId);
            TempData["ErrorMessage"] = exception.Message;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Activity {ActivityId} could not be deleted from MySQL.", activityId);
            TempData["ErrorMessage"] = "The activity could not be deleted. Check the database connection and try again.";
        }

        return RedirectToAction(nameof(Index), new { search });
    }

    private async Task<IActionResult> ReturnFormWithErrorsAsync(
        ActivityFormViewModel form,
        string search,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        ActivityListViewModel page;
        try
        {
            page = await BuildPageAsync(search, divisionId, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The activities page could not be rebuilt after validation or save failed.");
            page = new ActivityListViewModel { Search = search, DatabaseError = exception.Message };
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The activities page could not be rebuilt after validation or save failed.");
            page = new ActivityListViewModel
            {
                Search = search,
                DatabaseError = "Activities could not be loaded. Check the database connection and activities table."
            };
        }

        SetPageMetadata();
        return View("Index", new ActivityListViewModel
        {
            Search = page.Search,
            Activities = page.Activities,
            Divisions = page.Divisions,
            CanChooseDivision = page.CanChooseDivision,
            Form = form,
            OpenForm = true,
            DatabaseError = page.DatabaseError
        });
    }

    private async Task<ActivityListViewModel> BuildPageAsync(
        string search,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        var isSuperAdmin = UserDivision.IsSuperAdmin(User);
        return new ActivityListViewModel
        {
            Search = search,
            Activities = await activityRepository.GetAllAsync(search, divisionId, cancellationToken),
            Divisions = await divisionRepository.GetAllAsync(cancellationToken),
            CanChooseDivision = isSuperAdmin,
            Form = new ActivityFormViewModel { DivisionId = divisionId }
        };
    }

    private void SetPageMetadata()
    {
        ViewData["Title"] = "Activities";
        ViewData["Description"] = "Manage organization activities and events.";
        ViewData["ActiveSection"] = "activities";
    }
}
