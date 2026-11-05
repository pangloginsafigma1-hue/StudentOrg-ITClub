using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ITClubOrg.Models;

public sealed class SettingsViewModel : IValidatableObject
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(254)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [StringLength(20)]
    [Display(Name = "Student ID")]
    public string? StudentId { get; set; }

    [StringLength(50)]
    public string? Course { get; set; }

    [StringLength(20)]
    [Display(Name = "Year Level")]
    public string? YearLevel { get; set; }

    [StringLength(50)]
    [Display(Name = "Position")]
    public string? Position { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date Joined")]
    public DateTime? DateJoined { get; set; }

    [BindNever]
    public string Role { get; set; } = string.Empty;

    [BindNever]
    public string? DivisionName { get; set; }

    [DataType(DataType.Password)]
    [StringLength(100)]
    [Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string? ConfirmPassword { get; set; }

    public IFormFile? Photo { get; set; }

    public bool HasPhoto { get; set; }

    public string? DatabaseError { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(NewPassword) && NewPassword.Length < 6)
        {
            yield return new ValidationResult(
                "The new password must be at least 6 characters.",
                [nameof(NewPassword)]);
        }

        if (!string.IsNullOrEmpty(NewPassword)
            && !string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "The new password and confirmation do not match.",
                [nameof(ConfirmPassword)]);
        }

        if (!string.IsNullOrEmpty(Course) && !MemberLookups.Courses.Contains(Course, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Choose a valid course.", [nameof(Course)]);
        }

        if (!string.IsNullOrEmpty(YearLevel) && !MemberLookups.YearLevels.Contains(YearLevel, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Choose a valid year level.", [nameof(YearLevel)]);
        }
    }
}
