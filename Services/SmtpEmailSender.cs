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
        var host = configuration["Email:SmtpHost"];
        var username = configuration["Email:SmtpUsername"];
        var password = configuration["Email:SmtpPassword"];
        var fromAddress = configuration["Email:FromAddress"];
        var port = configuration.GetValue<int?>("Email:SmtpPort") ?? 587;

        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromAddress)
            || port is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "Password recovery email is not configured. Set Email:SmtpHost, SmtpPort, SmtpUsername, SmtpPassword, and FromAddress.");
        }

        using var message = new MailMessage(fromAddress, email)
        {
            Subject = "SOMAMS Password Reset Code",
            Body = $"Your SOMAMS verification code is: {code}{Environment.NewLine}{Environment.NewLine}" +
                   "This code expires in 10 minutes. If you did not request it, you can ignore this email."
        };
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = configuration.GetValue<bool?>("Email:EnableSsl") ?? true,
            Credentials = new NetworkCredential(username, password),
            Timeout = 15000
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
