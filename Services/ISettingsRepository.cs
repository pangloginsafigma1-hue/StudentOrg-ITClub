using ITClubOrg.Models;

namespace ITClubOrg.Services;

public interface ISettingsRepository
{
    Task<SettingsViewModel?> GetProfileAsync(int adminId, CancellationToken cancellationToken);

    Task<bool> IsUsernameTakenAsync(
        string username,
        int exceptAdminId,
        CancellationToken cancellationToken);

    Task<byte[]?> GetPhotoAsync(int adminId, CancellationToken cancellationToken);

    Task UpdateProfileAsync(
        int adminId,
        SettingsViewModel profile,
        byte[]? photo,
        CancellationToken cancellationToken);
}
