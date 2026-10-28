using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class ActivityFormViewModel
{
    public int ActivityId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a division.")]
    [Display(Name = "Send activity to")]
    public int? DivisionId { get; set; }

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public bool CanChooseDivision { get; init; }

    [Required]
    [StringLength(150, MinimumLength = 3)]
    [Display(Name = "Activity Name")]
    public string ActivityName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime ActivityDate { get; set; } = DateTime.Today;

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = "Upcoming";

    [StringLength(65535)]
    [Display(Name = "Description (optional)")]
    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<string> Statuses { get; init; } = MemberLookups.ActivityStatuses;
}
