using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize]
public sealed class AttendanceController(
    IActivityRepository activityRepository,
    IAttendanceRepository attendanceRepository,
    ILogger<AttendanceController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? activityId, CancellationToken cancellationToken)
    {
        var model = new AttendanceViewModel();
        try
        {
            model = await BuildPageAsync(activityId, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Attendance could not be loaded due to invalid database configuration.");
            model = new AttendanceViewModel { DatabaseError = exception.Message };
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Attendance could not be loaded from MySQL.");
            model = new AttendanceViewModel
            {
                DatabaseError = "Attendance could not be loaded. Check the database connection and required tables."
            };
        }

        SetPageMetadata();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        int activityId,
        string[]? presentMemberIds,
        CancellationToken cancellationToken)
    {
        if (activityId <= 0)
        {
            return BadRequest();
        }

        try
        {
            var saved = await attendanceRepository.SaveAsync(
                activityId,
                presentMemberIds ?? [],
                UserDivision.GetDivisionId(User),
                cancellationToken);

            if (!saved)
            {
                TempData["ErrorMessage"] =
                    "Attendance could not be saved. The activity may already be locked or the selected members are no longer active.";
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                "Attendance saved. This activity is now locked and can be found in Reports > Activity Attendance Report.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Attendance could not be saved due to invalid database configuration.");
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(Index), new { activityId });
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Attendance for activity {ActivityId} could not be saved to MySQL.", activityId);
            TempData["ErrorMessage"] = "Attendance could not be saved. Check the database connection and try again.";
            return RedirectToAction(nameof(Index), new { activityId });
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<AttendanceViewModel> BuildPageAsync(
        int? activityId,
        CancellationToken cancellationToken)
    {
        var divisionId = UserDivision.GetDivisionId(User);
        var activities = await activityRepository.GetPendingAttendanceAsync(divisionId, cancellationToken);
        if (!activityId.HasValue)
        {
            return new AttendanceViewModel { Activities = activities };
        }

        var selectedActivity = activities.FirstOrDefault(activity => activity.ActivityId == activityId.Value);
        if (selectedActivity is null)
        {
            return new AttendanceViewModel
            {
                Activities = activities,
                DatabaseError = "This activity is not available for attendance. It may already have recorded attendance."
            };
        }

        return new AttendanceViewModel
        {
            Activities = activities,
            SelectedActivityId = selectedActivity.ActivityId,
            SelectedActivityName = selectedActivity.ActivityName,
            Members = await attendanceRepository.GetRosterAsync(selectedActivity.ActivityId, divisionId, cancellationToken)
        };
    }

    private void SetPageMetadata()
    {
        ViewData["Title"] = "Attendance";
        ViewData["Description"] = "Record member attendance during an activity.";
        ViewData["ActiveSection"] = "attendance";
    }
}
