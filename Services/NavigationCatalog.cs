using ITClubOrg.Models;

namespace ITClubOrg.Services;

public static class NavigationCatalog
{
    public static IReadOnlyList<NavigationItem> Items { get; } =
    [
        new() { Key = "dashboard", Title = "Dashboard", Description = "Division or organization overview" },
        new() { Key = "members", Title = "Members", Description = "Search and manage division member records", IconPath = "/images/ic_members.png" },
        new() { Key = "activities", Title = "Activities", Description = "Manage activities and events for a division.", IconPath = "/images/ic_activities.png" },
        new() { Key = "attendance", Title = "Attendance", Description = "Record division member attendance during an activity.", IconPath = "/images/ic_attendance.png" },
        new() { Key = "membership", Title = "Membership", Description = "Manage division member records", IconPath = "/images/ic_membership.png" },
        new() { Key = "reports", Title = "Reports", Description = "Review and export organization reports", IconPath = "/images/ic_reports.png" },
        new() { Key = "settings", Title = "Settings", Description = "Manage administrator profile and preferences" },
        new() { Key = "admin-accounts", Title = "Admin Accounts", Description = "Manage administrator accounts and division access" }
    ];

    public static NavigationItem? Find(string key) =>
        Items.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
}
