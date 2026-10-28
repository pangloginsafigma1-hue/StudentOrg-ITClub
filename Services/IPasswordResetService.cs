namespace ITClubOrg.Services;

public interface IPasswordResetService
{
    Task<bool> SendCodeAsync(
        string flowToken,
        string email,
        string remoteAddress,
        CancellationToken cancellationToken);

    bool VerifyCode(string flowToken, string code, out int attemptsRemaining);

    Task<bool> ResetPasswordAsync(
        string flowToken,
        string newPassword,
        CancellationToken cancellationToken);
}
