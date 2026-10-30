using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class ReportsViewModel
{
    public IReadOnlyList<ReportMemberOption> Members { get; init; } = [];

    public IReadOnlyList<ReportActivityOption> RecordedActivities { get; init; } = [];

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public IReadOnlyList<int> DivisionIds { get; init; } = [];

    public bool CanChooseDivision { get; init; }

    public IReadOnlyList<string> Positions { get; init; } = MemberLookups.Positions;

    public IReadOnlyList<string> Statuses { get; init; } = MemberLookups.Statuses;

    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateTime FromDate { get; set; } = DateTime.Today;

    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateTime ToDate { get; set; } = DateTime.Today;

    public string? DatabaseError { get; init; }
}
