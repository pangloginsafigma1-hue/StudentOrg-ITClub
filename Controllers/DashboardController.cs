using ITClubOrg.Models;
using ITClubOrg.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace ITClubOrg.Controllers;

[Authorize]
public sealed class DashboardController(
    IDashboardRepository dashboardRepository,
    ILogger<DashboardController> logger) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = new DashboardViewModel
        {
            AdminName = User.Identity?.Name ?? "Admin"
        };

        try
        {
            var data = await dashboardRepository.GetDashboardDataAsync(
                UserDivision.GetDivisionId(User),
                cancellationToken);
            model = new DashboardViewModel
            {
                AdminName = model.AdminName,
                MemberCount = data.MemberCount,
                ActiveMemberCount = data.ActiveMemberCount,
                ActivityCount = data.ActivityCount,
                UpcomingActivityCount = data.UpcomingActivityCount,
                NewMemberCount30Days = data.NewMemberCount30Days,
                AttendanceRecordedCount = data.AttendanceRecordedCount,
                UpcomingActivities = data.UpcomingActivities,
                RecentActivities = data.RecentActivities,
                RecentMembers = data.RecentMembers,
                MonthlyTrend = data.MonthlyTrend
            };
        }
        catch (Exception exception) when (exception is MySqlException or InvalidOperationException)
        {
            logger.LogError(exception, "SOMAMS dashboard data could not be loaded.");
            model = new DashboardViewModel
            {
                AdminName = model.AdminName,
                DatabaseError = "Dashboard data could not be loaded. Check the database connection and schema."
            };
        }

        ViewData["Title"] = "Dashboard";
        ViewData["Description"] = "Organization overview";
        ViewData["ActiveSection"] = "dashboard";
        return View(model);
    }
}
