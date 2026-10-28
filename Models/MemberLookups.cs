namespace ITClubOrg.Models;

public static class MemberLookups
{
    public static IReadOnlyList<string> Positions { get; } =
    [
        "President",
        "Vice President",
        "Secretary",
        "Treasurer",
        "Auditor",
        "Public Information Officer",
        "Representative",
        "Member"
    ];

    public static IReadOnlyList<string> Courses { get; } = ["BSCS", "BSIT", "BSIS"];

    public static IReadOnlyList<string> YearLevels { get; } = ["1st Year", "2nd Year", "3rd Year", "4th Year"];

    public static IReadOnlyList<string> Statuses { get; } = ["Active", "Inactive"];

    public static IReadOnlyList<string> ActivityStatuses { get; } = ["Upcoming", "Pending", "Completed"];
}
