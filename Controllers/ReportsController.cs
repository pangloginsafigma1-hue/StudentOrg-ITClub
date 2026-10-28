using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize]
public sealed class ReportsController(
    IReportsRepository reportsRepository,
    ILogger<ReportsController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var divisionId = UserDivision.GetDivisionId(User);
        var model = new ReportsViewModel();
        try
        {
            model = new ReportsViewModel
            {
                Members = await reportsRepository.GetMembersAsync(divisionId, cancellationToken),
                RecordedActivities = await reportsRepository.GetRecordedActivitiesAsync(divisionId, cancellationToken)
            };
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "The Reports page could not connect to the configured database.");
            model = new ReportsViewModel { DatabaseError = exception.Message };
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "The Reports page could not load report filter options.");
            model = new ReportsViewModel
            {
                DatabaseError = "Report options could not be loaded. Check the MySQL connection and database schema."
            };
        }

        ViewData["Title"] = "Reports";
        ViewData["Description"] = "Review and export organization reports.";
        ViewData["ActiveSection"] = "reports";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ExportAllMembers(CancellationToken cancellationToken) =>
        ExportMembersAsync(null, null, null, "List of Organization Members", cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportFilteredMembers(
        string? memberId,
        string? position,
        string? status,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(position) && !MemberLookups.Positions.Contains(position, StringComparer.Ordinal)
            || !string.IsNullOrEmpty(status) && !MemberLookups.Statuses.Contains(status, StringComparer.Ordinal)
            || memberId?.Length > 20)
        {
            return BadRequest("The selected member filters are invalid.");
        }

        return await ExportMembersAsync(
            string.IsNullOrEmpty(memberId) ? null : memberId,
            string.IsNullOrEmpty(position) ? null : position,
            string.IsNullOrEmpty(status) ? null : status,
            "Organization Members Report",
            cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportActivities(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate == default || toDate == default || fromDate.Date > toDate.Date)
        {
            TempData["ErrorMessage"] = "The From date must not be later than the To date.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var report = await reportsRepository.GetActivitiesReportAsync(
                fromDate,
                toDate,
                UserDivision.GetDivisionId(User),
                cancellationToken);
            var title = $"List of Activities ({fromDate:MMM d, yyyy} - {toDate:MMM d, yyyy})";
            return Export(report, title, "SOMAMS-Activities.docx");
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The activities report could not be created.");
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The activities report could not be loaded from MySQL.");
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportAttendance(int activityId, CancellationToken cancellationToken)
    {
        if (activityId <= 0)
        {
            return BadRequest("Choose an activity with recorded attendance.");
        }

        try
        {
            var report = await reportsRepository.GetAttendanceReportAsync(
                activityId,
                UserDivision.GetDivisionId(User),
                cancellationToken);
            if (report is null)
            {
                TempData["ErrorMessage"] = "That activity is unavailable or its attendance has not been recorded.";
                return RedirectToAction(nameof(Index));
            }

            return Export(report, "Activity Attendance Report", $"SOMAMS-Attendance-{activityId}.docx");
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The attendance report could not be created.");
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The attendance report could not be loaded from MySQL.");
        }
    }

    private async Task<IActionResult> ExportMembersAsync(
        string? memberId,
        string? position,
        string? status,
        string title,
        CancellationToken cancellationToken)
    {
        try
        {
            var report = await reportsRepository.GetMembersReportAsync(
                memberId,
                position,
                status,
                UserDivision.GetDivisionId(User),
                cancellationToken);
            return Export(report, title, "SOMAMS-Members.docx");
        }
        catch (InvalidOperationException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be created.");
        }
        catch (MySqlException exception)
        {
            return HandleDatabaseError(exception, "The members report could not be loaded from MySQL.");
        }
    }

    private IActionResult Export(ReportTable report, string title, string fileName)
    {
        if (report.Rows.Count == 0)
        {
            TempData["ErrorMessage"] = "There is no data to export for the selected report.";
            return RedirectToAction(nameof(Index));
        }

        return File(
            ReportWordExporter.Export(title, report),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            fileName);
    }

    private IActionResult HandleDatabaseError(Exception exception, string message)
    {
        logger.LogError(exception, "A report export failed.");
        TempData["ErrorMessage"] = message;
        return RedirectToAction(nameof(Index));
    }
}
