using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace ITClubOrg.Models;

public sealed class ForgotPasswordViewModel
{
    public string Step { get; set; } = "email";

    [Required]
    [StringLength(64, MinimumLength = 64)]
    public string? FlowToken { get; set; }

    public string? Email { get; set; }

    [Display(Name = "Verification code")]
    public string? Code { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    public string? ConfirmPassword { get; set; }

    public string? Message { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FlowToken is null || FlowToken.Length != 64 || !FlowToken.All(Uri.IsHexDigit))
        {
            yield return new ValidationResult("The recovery session is invalid. Start again.", [nameof(FlowToken)]);
        }

        if (Step == "email")
        {
            var email = Email?.Trim() ?? string.Empty;
            if (email.Length > 100 || !MailAddress.TryCreate(email, out var address)
                || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                yield return new ValidationResult("Enter a valid email address.", [nameof(Email)]);
            }
        }

        if (Step == "code" && !System.Text.RegularExpressions.Regex.IsMatch(Code ?? string.Empty, @"^\d{6}$"))
        {
            yield return new ValidationResult("Enter the 6-digit verification code.", [nameof(Code)]);
        }

        if (Step == "password"
            && (string.IsNullOrEmpty(NewPassword) || NewPassword.Length is < 6 or > 64))
        {
            yield return new ValidationResult("The password must be between 6 and 64 characters.", [nameof(NewPassword)]);
        }

        if (Step == "password"
            && !string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            yield return new ValidationResult(
                "The passwords do not match.",
                [nameof(ConfirmPassword)]);
        }
    }
}
