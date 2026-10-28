using System.ComponentModel.DataAnnotations;

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
    [StringLength(100)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

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
    }
}
