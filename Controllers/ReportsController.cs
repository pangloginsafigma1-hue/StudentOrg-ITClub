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
    public async Task<IActionResult> Index(int[]? divisionIds, CancellationToken cancellationToken)
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
                var selectedIds = NormalizeDivisionIds(divisionIds);
                if (selectedIds.Count > 2 || selectedIds.Any(id => !divisions.Any(division => division.DivisionId == id)))
                {
                    return BadRequest("Choose a valid division.");
                }

                selectedDivisionId = selectedIds.Count == 1 ? selectedIds[0] : null;
            }

            model = new ReportsViewModel
            {
                Members = await reportsRepository.GetMembersAsync(canChooseDivision ? NormalizeDivisionIds(divisionIds) : ToDivisionList(selectedDivisionId), cancellationToken),
                RecordedActivities = await reportsRepository.GetRecordedActivitiesAsync(canChooseDivision ? NormalizeDivisionIds(divisionIds) : ToDivisionList(selectedDivisionId), cancellationToken),
                Divisions = divisions,
                DivisionIds = canChooseDivision ? NormalizeDivisionIds(divisionIds) : ToDivisionList(selectedDivisionId),
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
                DivisionIds = ToDivisionList(selectedDivisionId),
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
                DivisionIds = ToDivisionList(selectedDivisionId),
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
    public async Task<IActionResult> ExportAllMembers(int[]? divisionIds, CancellationToken cancellationToken)
    {
        try
        {
            var selection = await ResolveDivisionIdsAsync(divisionIds, cancellationToken);
            if (!selection.IsValid)
            {
                return BadRequest("Choose a valid division.");
            }

            return await ExportMembersAsync(
                null,
                null,
                null,
                selection.DivisionIds,
                "List of Organization Members",
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be created.", divisionIds);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be loaded from MySQL.", divisionIds);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportFilteredMembers(
        string? memberId,
        string? position,
        string? status,
        int[]? divisionIds,
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
            var selection = await ResolveDivisionIdsAsync(divisionIds, cancellationToken);
            if (!selection.IsValid)
            {
                return BadRequest("Choose a valid division.");
            }

            return await ExportMembersAsync(
                string.IsNullOrEmpty(memberId) ? null : memberId,
                string.IsNullOrEmpty(position) ? null : position,
                string.IsNullOrEmpty(status) ? null : status,
                selection.DivisionIds,
                "Organization Members Report",
                cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be created.", divisionIds);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be loaded from MySQL.", divisionIds);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportActivities(
        DateTime fromDate,
        DateTime toDate,
        int[]? divisionIds,
        CancellationToken cancellationToken)
    {
        if (fromDate == default || toDate == default || fromDate.Date > toDate.Date)
        {
            TempData["ErrorMessage"] = "The From date must not be later than the To date.";
            return RedirectToAction(nameof(Index), new { divisionIds });
        }

        try
        {
            var selection = await ResolveDivisionIdsAsync(divisionIds, cancellationToken);
            if (!selection.IsValid)
            {
                return BadRequest("Choose a valid division.");
            }

            var report = await reportsRepository.GetActivitiesReportAsync(
                fromDate,
                toDate,
                selection.DivisionIds,
                cancellationToken);
            var title = $"List of Activities ({fromDate:MMM d, yyyy} - {toDate:MMM d, yyyy})";
            return Export(report, title, "SOMAMS-Activities.docx", selection.DivisionIds);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The activities report could not be created.", divisionIds);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The activities report could not be loaded from MySQL.", divisionIds);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportAttendance(
        int activityId,
        int[]? divisionIds,
        CancellationToken cancellationToken)
    {
        if (activityId <= 0)
        {
            return BadRequest("Choose an activity with recorded attendance.");
        }

        try
        {
            var selection = await ResolveDivisionIdsAsync(divisionIds, cancellationToken);
            if (!selection.IsValid)
            {
                return BadRequest("Choose a valid division.");
            }

            var report = await reportsRepository.GetAttendanceReportAsync(
                activityId,
                selection.DivisionIds,
                cancellationToken);
            if (report is null)
            {
                TempData["ErrorMessage"] = "That activity is unavailable or its attendance has not been recorded.";
                return RedirectToAction(nameof(Index), new { divisionIds });
            }

            return Export(report, "Activity Attendance Report", $"SOMAMS-Attendance-{activityId}.docx", selection.DivisionIds);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The attendance report could not be created.", divisionIds);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The attendance report could not be loaded from MySQL.", divisionIds);
        }
    }

    private async Task<IActionResult> ExportMembersAsync(
        string? memberId,
        string? position,
        string? status,
        IReadOnlyList<int> divisionIds,
        string title,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await reportsRepository.GetMembersReportAsync(
                memberId,
                position,
                status,
                divisionIds,
                cancellationToken);
            return Export(report, title, "SOMAMS-Members.docx", divisionIds);
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be created.", divisionIds);
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be loaded from MySQL.", divisionIds);
        }
    }

    private async Task<(IReadOnlyList<int> DivisionIds, bool IsValid)> ResolveDivisionIdsAsync(
        int[]? requestedDivisionIds,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole("SuperAdmin"))
        {
            return (ToDivisionList(UserDivision.GetDivisionId(User)), true);
        }

        var selectedIds = NormalizeDivisionIds(requestedDivisionIds);
        if (selectedIds.Count == 0)
        {
            return ([], true);
        }

        var divisions = await divisionRepository.GetAllAsync(cancellationToken);
        return selectedIds.Count <= 2 && selectedIds.All(id => divisions.Any(division => division.DivisionId == id))
            ? (selectedIds, true)
            : ([], false);
    }

    private IActionResult Export(ReportTable report, string title, string fileName, IReadOnlyList<int> divisionIds)
    {
        if (report.Rows.Count == 0)
        {
            TempData["ErrorMessage"] = "There is no data to export for the selected report.";
            return RedirectToAction(nameof(Index), new { divisionIds });
        }

        return File(
            ReportWordExporter.Export(title, report),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    private IActionResult HandleDatabaseError(Exception exception, string message, IReadOnlyList<int>? divisionIds = null)
    {
        logger.LogError(exception, "A report export failed.");
        TempData["ErrorMessage"] = message;
        return RedirectToAction(nameof(Index), new { divisionIds });
    }

    private static IReadOnlyList<int> NormalizeDivisionIds(IEnumerable<int>? divisionIds) =>
        divisionIds?.Where(id => id > 0).Distinct().ToArray() ?? [];

    private static IReadOnlyList<int> ToDivisionList(int? divisionId) =>
        divisionId is int id ? [id] : [];
}
