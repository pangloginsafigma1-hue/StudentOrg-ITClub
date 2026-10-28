namespace ITClubOrg.Services;

public interface IEmailSender
{
    Task SendPasswordResetCodeAsync(string email, string code, CancellationToken cancellationToken);
}
