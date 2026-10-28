using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class MemberFormViewModel
{
    [Required]
    [StringLength(20)]
    [Display(Name = "Membership ID")]
    public string MemberId { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a division.")]
    [Display(Name = "Division")]
    public int? DivisionId { get; set; }

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public bool CanChooseDivision { get; init; }

    [Required]
    [StringLength(100)]
    [RegularExpression(@"^\p{L}[\p{L}\s.'-]{1,99}$", ErrorMessage = "Enter a valid name using letters, spaces, periods, apostrophes, or hyphens.")]
    [Display(Name = "Student Name")]
    public string StudentName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Course { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    [Display(Name = "Year Level")]
    public string YearLevel { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Position { get; set; } = string.Empty;

    [Required]
    [StringLength(8)]
    public string Status { get; set; } = "Active";

    [DataType(DataType.Date)]
    [Display(Name = "Date Joined")]
    public DateTime DateJoined { get; set; } = DateTime.Today;

    public bool IsEdit { get; init; }

    public string StudentIdExample { get; init; } = "26-0001";

    public IReadOnlyList<string> Courses { get; init; } = MemberLookups.Courses;

    public IReadOnlyList<string> YearLevels { get; init; } = MemberLookups.YearLevels;

    public IReadOnlyList<string> Positions { get; init; } = MemberLookups.Positions;

    public IReadOnlyList<string> Statuses { get; init; } = MemberLookups.Statuses;
}
