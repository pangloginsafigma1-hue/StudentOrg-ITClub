using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize(Roles = "SuperAdmin,DivisionAdmin")]
public sealed class ReportsController(
    IReportsRepository reportsRepository,
    IDivisionRepository divisionRepository,
    ILogger<ReportsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? divisionId, CancellationToken cancellationToken)
    {
        var canChooseDivision = User.IsInRole("SuperAdmin");
        var selectedDivisionId = UserDivision.GetDivisionId(User);
        IReadOnlyList<Division> divisions = [];
        var model = new ReportsViewModel();
        try
        {
            if (canChooseDivision)
            {
                divisions = await divisionRepository.GetAllAsync(cancellationToken);
                if (divisionId.HasValue && !divisions.Any(division => division.DivisionId == divisionId.Value))
                {
                    return BadRequest("Choose a valid division.");
                }

                selectedDivisionId = divisionId;
            }

            model = new ReportsViewModel
            {
                Members = await reportsRepository.GetMembersAsync(selectedDivisionId, cancellationToken),
                RecordedActivities = await reportsRepository.GetRecordedActivitiesAsync(selectedDivisionId, cancellationToken),
                Divisions = divisions,
                DivisionId = selectedDivisionId,
                CanChooseDivision = canChooseDivision
            };
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The Reports page could not connect to the configured database.");
            model = new ReportsViewModel
            {
                DatabaseError = exception.Message,
                Divisions = divisions,
                DivisionId = selectedDivisionId,
                CanChooseDivision = canChooseDivision
            };
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The Reports page could not load report filter options.");
            model = new ReportsViewModel
            {
                DatabaseError = "Report options could not be loaded. Check the MySQL connection and database schema.",
                Divisions = divisions,
                DivisionId = selectedDivisionId,
                CanChooseDivision = canChooseDivision
            };
        }

        ViewData["Title"] = "Reports";
        ViewData["Description"] = "Review and export organization reports.";
        ViewData["ActiveSection"] = "reports";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportAllMembers(int? divisionId, CancellationToken cancellationToken)
    {
        try
        {
            var selection = await ResolveDivisionIdAsync(divisionId, cancellationToken);
            if (!selection.IsValid)
            {
                return BadRequest("Choose a valid division.");
            }

            return await ExportMembersAsync(
                null,
                null,
                null,
                selection.DivisionId,
                "List of Organization Members",
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be created.", divisionId);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be loaded from MySQL.", divisionId);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportFilteredMembers(
        string? memberId,
        string? position,
        string? status,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(position) && !MemberLookups.Positions.Contains(position, StringComparer.Ordinal)
            || !string.IsNullOrEmpty(status) && !MemberLookups.Statuses.Contains(status, StringComparer.Ordinal)
            || memberId?.Length > 20)
        {
            return BadRequest("The selected member filters are invalid.");
        }

        try
        {
            var selection = await ResolveDivisionIdAsync(divisionId, cancellationToken);
            if (!selection.IsValid)
            {
                return BadRequest("Choose a valid division.");
            }

            return await ExportMembersAsync(
                string.IsNullOrEmpty(memberId) ? null : memberId,
                string.IsNullOrEmpty(position) ? null : position,
                string.IsNullOrEmpty(status) ? null : status,
                selection.DivisionId,
                "Organization Members Report",
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be created.", divisionId);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be loaded from MySQL.", divisionId);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportActivities(
        DateTime fromDate,
        DateTime toDate,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        if (fromDate == default || toDate == default || fromDate.Date > toDate.Date)
        {
            TempData["ErrorMessage"] = "The From date must not be later than the To date.";
            return RedirectToAction(nameof(Index), new { divisionId });
        }

        try
        {
            var selection = await ResolveDivisionIdAsync(divisionId, cancellationToken);
            if (!selection.IsValid)
            {
                return BadRequest("Choose a valid division.");
            }

            var report = await reportsRepository.GetActivitiesReportAsync(
                fromDate,
                toDate,
                selection.DivisionId,
                cancellationToken);
            var title = $"List of Activities ({fromDate:MMM d, yyyy} - {toDate:MMM d, yyyy})";
            return Export(report, title, "SOMAMS-Activities.docx", selection.DivisionId);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The activities report could not be created.", divisionId);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The activities report could not be loaded from MySQL.", divisionId);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportAttendance(
        int activityId,
        int? divisionId,
        CancellationToken cancellationToken)
    {
        if (activityId <= 0)
        {
            return BadRequest("Choose an activity with recorded attendance.");
        }

        try
        {
            var selection = await ResolveDivisionIdAsync(divisionId, cancellationToken);
            if (!selection.IsValid)
            {
                return BadRequest("Choose a valid division.");
            }

            var report = await reportsRepository.GetAttendanceReportAsync(
                activityId,
                selection.DivisionId,
                cancellationToken);
            if (report is null)
            {
                TempData["ErrorMessage"] = "That activity is unavailable or its attendance has not been recorded.";
                return RedirectToAction(nameof(Index), new { divisionId });
            }

            return Export(report, "Activity Attendance Report", $"SOMAMS-Attendance-{activityId}.docx", selection.DivisionId);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The attendance report could not be created.", divisionId);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The attendance report could not be loaded from MySQL.", divisionId);
        }
    }

    private async Task<IActionResult> ExportMembersAsync(
        string? memberId,
        string? position,
        string? status,
        int? divisionId,
        string title,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await reportsRepository.GetMembersReportAsync(
                memberId,
                position,
                status,
                divisionId,
                cancellationToken);
            return Export(report, title, "SOMAMS-Members.docx", divisionId);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be created.", divisionId);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be loaded from MySQL.", divisionId);
        }
    }

    private async Task<(int? DivisionId, bool IsValid)> ResolveDivisionIdAsync(
        int? requestedDivisionId,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole("SuperAdmin"))
        {
            return (UserDivision.GetDivisionId(User), true);
        }

        if (!requestedDivisionId.HasValue)
        {
            return (null, true);
        }

        var divisions = await divisionRepository.GetAllAsync(cancellationToken);
        return divisions.Any(division => division.DivisionId == requestedDivisionId.Value)
            ? (requestedDivisionId, true)
            : (null, false);
    }

    private IActionResult Export(ReportTable report, string title, string fileName, int? divisionId)
    {
        if (report.Rows.Count == 0)
        {
            TempData["ErrorMessage"] = "There is no data to export for the selected report.";
            return RedirectToAction(nameof(Index), new { divisionId });
        }

        return File(
            ReportWordExporter.Export(title, report),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    private IActionResult HandleDatabaseError(Exception exception, string message, int? divisionId = null)
    {
        logger.LogError(exception, "A report export failed.");
        TempData["ErrorMessage"] = message;
        return RedirectToAction(nameof(Index), new { divisionId });
    }
}
