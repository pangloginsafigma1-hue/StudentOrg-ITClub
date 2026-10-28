using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface IPasswordResetRepository
{
    Task<PasswordResetAccount?> GetAccountByEmailAsync(string email, CancellationToken cancellationToken);

    Task<bool> ResetPasswordAsync(int adminId, string newPassword, CancellationToken cancellationToken);
}
