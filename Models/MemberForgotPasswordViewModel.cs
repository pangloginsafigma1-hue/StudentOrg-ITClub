using System.ComponentModel.DataAnnotations;
using System.Net.Mail;

namespace ITClubOrg.Models;

public sealed class MemberForgotPasswordViewModel : IValidatableObject
{
    public string Step { get; set; } = "email";

    [Required, StringLength(64)]
    public string FlowToken { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Confirm new password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? Message { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FlowToken.Length != 64 || !FlowToken.All(Uri.IsHexDigit))
            yield return new ValidationResult("The recovery session is invalid. Start again.", [nameof(FlowToken)]);
        if (Step == "email" && (!MailAddress.TryCreate(Email.Trim(), out var address)
            || !string.Equals(address.Address, Email.Trim(), StringComparison.OrdinalIgnoreCase)))
            yield return new ValidationResult("Enter a valid email address.", [nameof(Email)]);
        if (Step == "code" && !System.Text.RegularExpressions.Regex.IsMatch(Code, @"^\d{6}$"))
            yield return new ValidationResult("Enter the 6-digit verification code.", [nameof(Code)]);
        if (Step == "password")
        {
            if (NewPassword.Length is < 8 or > 72)
                yield return new ValidationResult("Use a password between 8 and 72 characters.", [nameof(NewPassword)]);
            if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
                yield return new ValidationResult("The passwords do not match.", [nameof(ConfirmPassword)]);
        }
    }
}
