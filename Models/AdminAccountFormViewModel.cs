using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class AdminAccountFormViewModel : IValidatableObject
{
    public int AdminId { get; set; }

    public string? SourceMemberId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[\p{L}\p{N}._-]+$", ErrorMessage = "Use letters, numbers, periods, underscores, or hyphens.")]
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
    public DateTime? DateJoined { get; set; } = DateTime.Today;

    [Required]
    [StringLength(30)]
    public string Role { get; set; } = "DivisionAdmin";

    public bool CanSelectSuperAdmin { get; set; } = true;

    [Range(1, int.MaxValue, ErrorMessage = "Choose a division for a division admin.")]
    [Display(Name = "Division")]
    public int? DivisionId { get; set; }

    [DataType(DataType.Password)]
    [StringLength(100)]
    [Display(Name = "Temporary password")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    public string? ConfirmPassword { get; set; }

    [Display(Name = "Account enabled")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => AdminId > 0;

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public IReadOnlyList<string> Courses { get; init; } = MemberLookups.Courses;

    public IReadOnlyList<string> YearLevels { get; init; } = MemberLookups.YearLevels;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrEmpty(Course) && !MemberLookups.Courses.Contains(Course, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Choose a valid course.", [nameof(Course)]);
        }

        if (!string.IsNullOrEmpty(YearLevel) && !MemberLookups.YearLevels.Contains(YearLevel, StringComparer.Ordinal))
        {
            yield return new ValidationResult("Choose a valid year level.", [nameof(YearLevel)]);
        }

        if (Role == "DivisionAdmin" && DivisionId is null)
        {
            yield return new ValidationResult("Choose a division for a division admin.", [nameof(DivisionId)]);
        }

        if (Role == "SuperAdmin" && DivisionId is not null)
        {
            yield return new ValidationResult("A super admin cannot be assigned to a division.", [nameof(DivisionId)]);
        }

        if (Role is "Member" && !IsEdit)
        {
            yield return new ValidationResult("Member is only available when managing an existing administrator.", [nameof(Role)]);
        }

        if (Role == "Member" && string.IsNullOrWhiteSpace(StudentId))
        {
            yield return new ValidationResult("A student ID is required to convert this account to a member.", [nameof(StudentId)]);
        }

        if (Role is not ("SuperAdmin" or "DivisionAdmin" or "Member"))
        {
            yield return new ValidationResult("Choose a valid administrator role.", [nameof(Role)]);
        }

        if (!IsEdit && string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult("A temporary password is required.", [nameof(Password)]);
        }

        if (!string.IsNullOrEmpty(Password) && Password.Length < 6)
        {
            yield return new ValidationResult("Use a password of at least 6 characters.", [nameof(Password)]);
        }

        if (!string.IsNullOrEmpty(Password)
            && !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            yield return new ValidationResult("The passwords do not match.", [nameof(ConfirmPassword)]);
        }

        if (IsEdit
            && string.IsNullOrEmpty(Password)
            && !string.IsNullOrEmpty(ConfirmPassword))
        {
            yield return new ValidationResult(
                "Leave both password fields blank to keep the current password.",
                [nameof(ConfirmPassword)]);
        }
    }
}
