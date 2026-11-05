namespace ITClubOrg.Models;

public static class AdminReserveSlots
{
    public const string Software = "DivisionAdmin:Software";
    public const string Hardware = "DivisionAdmin:Hardware";
    public const string MultimediaAndArts = "DivisionAdmin:MultimediaAndArts";
    public const string SuperAdmin = "SuperAdmin";

    public static IReadOnlyList<(string Key, string Name)> All { get; } =
    [
        (Software, "Software Division Admin"),
        (Hardware, "Hardware Division Admin"),
        (MultimediaAndArts, "Multimedia & Arts Division Admin"),
        (SuperAdmin, "Super Admin")
    ];

    public static bool IsValid(string slotKey) =>
        All.Any(slot => string.Equals(slot.Key, slotKey, StringComparison.Ordinal));
}
