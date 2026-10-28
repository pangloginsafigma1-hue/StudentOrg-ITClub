using System.ComponentModel.DataAnnotations;

namespace ITClubOrg.Models;

public sealed class AdminAccountFormViewModel : IValidatableObject
{
    public int AdminId { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[\p{L}\p{N}._-]+$", ErrorMessage = "Use letters, numbers, periods, underscores, or hyphens.")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(30)]
    public string Role { get; set; } = "DivisionAdmin";

    [Range(1, int.MaxValue, ErrorMessage = "Choose a division for a division admin.")]
    [Display(Name = "Division")]
    public int? DivisionId { get; set; }

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 12)]
    [Display(Name = "Temporary password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Account enabled")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => AdminId > 0;

    public IReadOnlyList<Division> Divisions { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Role == "DivisionAdmin" && DivisionId is null)
        {
            yield return new ValidationResult("Choose a division for a division admin.", [nameof(DivisionId)]);
        }

        if (Role == "SuperAdmin" && DivisionId is not null)
        {
            yield return new ValidationResult("A super admin cannot be assigned to a division.", [nameof(DivisionId)]);
        }

        if (Role is not ("SuperAdmin" or "DivisionAdmin"))
        {
            yield return new ValidationResult("Choose a valid administrator role.", [nameof(Role)]);
        }

        if (!IsEdit && string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult("A temporary password is required.", [nameof(Password)]);
        }

        if (IsEdit && !string.IsNullOrEmpty(Password) && Password.Length < 12)
        {
            yield return new ValidationResult("Use a password of at least 12 characters.", [nameof(Password)]);
        }
    }
}
