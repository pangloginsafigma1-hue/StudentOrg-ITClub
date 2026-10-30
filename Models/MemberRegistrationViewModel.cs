using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class MemberRegistrationViewModel
{
    public string Step { get; set; } = "details";

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

    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public IReadOnlyList<string> Courses { get; init; } = MemberLookups.Courses;

    public IReadOnlyList<string> YearLevels { get; init; } = MemberLookups.YearLevels;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Step == "password")
        {
            if (string.IsNullOrEmpty(Password) || Password.Length is < 8 or > 72)
            {
                yield return new ValidationResult("Use a password between 8 and 72 characters.", [nameof(Password)]);
            }

            if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            {
                yield return new ValidationResult("The passwords do not match.", [nameof(ConfirmPassword)]);
            }
        }
    }
}
