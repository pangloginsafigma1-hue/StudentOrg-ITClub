using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class ActivityFormViewModel
{
    public int ActivityId { get; set; }

    [Display(Name = "Audience")]
    public int? DivisionId { get; set; }

    public int[] DivisionIds { get; set; } = [];

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public bool CanChooseDivision { get; init; }

    [Required]
    [StringLength(150, MinimumLength = 3)]
    [Display(Name = "Activity Name")]
    public string ActivityName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Starts")]
    public DateTime? ActivityDate { get; set; } = DateTime.Today.AddHours(9);

    [Required]
    [DataType(DataType.DateTime)]
    [Display(Name = "Ends")]
    public DateTime? ActivityEndDate { get; set; } = DateTime.Today.AddHours(10);

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Upcoming";

    [StringLength(65535)]
    [Display(Name = "Description (optional)")]
    public string? Description { get; set; }

    public IReadOnlyList<string> Statuses { get; init; } = MemberLookups.ActivityStatuses;
}
