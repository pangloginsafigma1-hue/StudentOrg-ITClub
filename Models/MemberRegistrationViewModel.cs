using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class MemberRegistrationViewModel
{
    [Required]
    [StringLength(20)]
    [Display(Name = "Student ID")]
    public string MemberId { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [RegularExpression(@"^\p{L}[\p{L}\s.'-]{1,99}$", ErrorMessage = "Enter a valid name.")]
    [Display(Name = "Full name")]
    public string StudentName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Course { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    [Display(Name = "Year level")]
    public string YearLevel { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a division.")]
    [Display(Name = "Division")]
    public int? DivisionId { get; set; }

    [Required]
    [StringLength(72, MinimumLength = 12)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password))]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public IReadOnlyList<string> Courses { get; init; } = MemberLookups.Courses;

    public IReadOnlyList<string> YearLevels { get; init; } = MemberLookups.YearLevels;
}
