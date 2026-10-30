using System.Net;
using System.Net.Mail;

namespace ITClubOrg.Services;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendPasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken)
    {
        using var client = CreateSmtpClient(out var fromAddress);
        using var message = new MailMessage(fromAddress, email)
        {
            Subject = "SOMAMS Password Reset Code",
            Body = $"Your SOMAMS verification code is: {code}{Environment.NewLine}{Environment.NewLine}" +
                   "This code expires in 10 minutes. If you did not request it, you can ignore this email."
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendRegistrationApprovedAsync(
        string email,
        string studentName,
        string memberId,
        CancellationToken cancellationToken)
    {
        using var client = CreateSmtpClient(out var fromAddress);
        using var message = new MailMessage(fromAddress, email)
        {
            Subject = "Your SOMAMS member portal access is approved",
            Body = $"Hello {studentName},{Environment.NewLine}{Environment.NewLine}" +
                   "Your membership registration has been approved. You can now sign in to the SOMAMS Member Portal " +
                   $"using your Student ID ({memberId}) and the password you created during registration." +
                   $"{Environment.NewLine}{Environment.NewLine}" +
                   "If you did not submit this registration, please contact your organization administrator."
        };

        await client.SendMailAsync(message, cancellationToken);
    }

    private SmtpClient CreateSmtpClient(out string fromAddress)
    {
        var host = configuration["Email:SmtpHost"];
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];
        fromAddress = configuration["Email:FromAddress"] ?? string.Empty;
        var port = configuration.GetValue<int?>("Email:SmtpPort") ?? 587;

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromAddress)
            || port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "Email delivery is not configured. Set Email:SmtpHost, SmtpPort, SmtpUsername, SmtpPassword, and FromAddress.");
        }

        return new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue<bool?>("Email:EnableSsl") ?? true,
            Credentials = new NetworkCredential(username, password),
            Timeout = 15000
        };
    }
}
