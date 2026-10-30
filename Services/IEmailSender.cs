namespace ITClubOrg.Services;

public interface IEmailSender
{
    Task SendPasswordResetCodeAsync(string email, string code, CancellationToken cancellationToken);

    Task SendRegistrationApprovedAsync(
        string email,
        string studentName,
        string memberId,
        CancellationToken cancellationToken);
}
