using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "SuperAdmin,DivisionAdmin")]
public sealed class AttendanceController(
    IActivityRepository activityRepository,
    IAttendanceRepository attendanceRepository,
    ILogger<AttendanceController> logger) : Controller
{
    private const int ActivityPageSize = 15;

    [HttpGet]
    public async Task<IActionResult> Index(int? activityId, int page = 1, CancellationToken cancellationToken = default)
    {
        var model = new AttendanceViewModel();
        try
        {
            model = await BuildPageAsync(activityId, page, cancellationToken);
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
        int page,
        string[]? presentMemberIds,
        CancellationToken cancellationToken)
    {
        if (activityId <= 0)
        {
            return BadRequest();
        }

        try
        {
            var result = await attendanceRepository.SaveAsync(
                activityId,
                presentMemberIds ?? [],
                UserDivision.GetDivisionId(User),
                UserDivision.IsSuperAdmin(User),
                cancellationToken);

            if (result != AttendanceSaveResult.Saved)
            {
                TempData["ErrorMessage"] = result switch
                {
                    AttendanceSaveResult.NotStarted => "Attendance cannot be recorded until the activity start time.",
                    AttendanceSaveResult.InvalidRoster => "Attendance could not be saved because one or more selected members are no longer on the activity roster.",
                    _ => "Attendance could not be saved. The activity may be unavailable or already locked."
                };
                return RedirectToAction(nameof(Index), new { activityId, page });
            }

            TempData["SuccessMessage"] = UserDivision.IsSuperAdmin(User)
                ? "Attendance saved or updated."
                : "Attendance saved. This activity is now locked and can be found in Reports > Activity Attendance Report.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Attendance could not be saved due to invalid database configuration.");
            TempData["ErrorMessage"] = exception.Message;
            return RedirectToAction(nameof(Index), new { activityId, page });
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Attendance for activity {ActivityId} could not be saved to MySQL.", activityId);
            TempData["ErrorMessage"] = "Attendance could not be saved. Check the database connection and try again.";
            return RedirectToAction(nameof(Index), new { activityId, page });
        }

        return RedirectToAction(nameof(Index), new { page });
    }

    private async Task<AttendanceViewModel> BuildPageAsync(
        int? activityId,
        int page,
        CancellationToken cancellationToken)
    {
        var divisionId = UserDivision.GetDivisionId(User);
        var canEditRecordedAttendance = UserDivision.IsSuperAdmin(User);
        var activities = await activityRepository.GetAttendanceActivitiesAsync(
            divisionId,
            canEditRecordedAttendance,
            cancellationToken);
        var pageCount = (int)Math.Ceiling(activities.Count / (double)ActivityPageSize);
        page = Math.Clamp(page, 1, Math.Max(pageCount, 1));
        var activityPage = activities
            .Skip((page - 1) * ActivityPageSize)
            .Take(ActivityPageSize)
            .Select(activity => new AttendanceActivityOption
            {
                ActivityId = activity.ActivityId,
                ActivityName = activity.ActivityName,
                ActivityDate = activity.ActivityDate,
                ActivityEndDate = activity.ActivityEndDate,
                AttendanceDone = activity.AttendanceDone,
                CanRecordAttendance = activity.ActivityDate <= DateTime.Now
                    || canEditRecordedAttendance && activity.AttendanceDone,
                DivisionName = activity.DivisionName
            })
            .ToArray();
        if (!activityId.HasValue)
        {
            return new AttendanceViewModel
            {
                Activities = activityPage,
                CanEditRecordedAttendance = canEditRecordedAttendance,
                ActivityPage = page,
                ActivityPageCount = pageCount,
                ActivityCount = activities.Count
            };
        }

        var selectedActivity = activities.FirstOrDefault(activity => activity.ActivityId == activityId.Value);
        if (selectedActivity is null)
        {
            return new AttendanceViewModel
            {
                Activities = activityPage,
                CanEditRecordedAttendance = canEditRecordedAttendance,
                ActivityPage = page,
                ActivityPageCount = pageCount,
                ActivityCount = activities.Count,
                DatabaseError = "This activity is not available for attendance. It may already have recorded attendance."
            };
        }

        if (!selectedActivity.AttendanceDone
            && selectedActivity.ActivityDate > DateTime.Now)
        {
            return new AttendanceViewModel
            {
                Activities = activityPage,
                CanEditRecordedAttendance = canEditRecordedAttendance,
                ActivityPage = page,
                ActivityPageCount = pageCount,
                ActivityCount = activities.Count,
                NotStartedActivityName = selectedActivity.ActivityName
            };
        }

        return new AttendanceViewModel
        {
            Activities = activityPage,
            SelectedActivityId = selectedActivity.ActivityId,
            SelectedActivityName = selectedActivity.ActivityName,
            SelectedActivityAttendanceDone = selectedActivity.AttendanceDone,
            CanEditRecordedAttendance = canEditRecordedAttendance,
            ActivityPage = page,
            ActivityPageCount = pageCount,
            ActivityCount = activities.Count,
            Members = selectedActivity.AttendanceDone
                ? await attendanceRepository.GetRecordedRosterAsync(selectedActivity.ActivityId, cancellationToken)
                : await attendanceRepository.GetRosterAsync(selectedActivity.ActivityId, divisionId, cancellationToken)
        };
    }

    private void SetPageMetadata()
    {
        ViewData["Title"] = "Attendance";
        ViewData["Description"] = UserDivision.IsSuperAdmin(User)
            ? "Record attendance for any division and correct recorded attendance."
            : "Record member attendance during an activity.";
        ViewData["ActiveSection"] = "attendance";
    }
}
