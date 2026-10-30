using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using ITClubOrg.Models;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class MemberPasswordResetService(
    IMemberPortalRepository repository,
    IEmailSender emailSender,
    IMemoryCache cache,
    ILogger<MemberPasswordResetService> logger) : IMemberPasswordResetService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestInterval = TimeSpan.FromMinutes(1);
    private static readonly object ChallengeLock = new();
    private const int MaximumAttempts = 3;

    public async Task<bool> SendCodeAsync(string flowToken, string email, string remoteAddress, CancellationToken cancellationToken)
    {
        var emailKey = RateKey("email", email.Trim().ToUpperInvariant());
        var addressKey = RateKey("address", remoteAddress);
        if (cache.TryGetValue(emailKey, out _) || cache.TryGetValue(addressKey, out _))
        {
            return false;
        }

        cache.Set(emailKey, true, RequestInterval);
        cache.Set(addressKey, true, RequestInterval);
        var account = await repository.GetMemberPasswordResetAccountByEmailAsync(email.Trim(), cancellationToken);
        if (account is null)
        {
            CacheChallenge(flowToken, string.Empty, RandomNumberGenerator.GetInt32(100000, 1000000).ToString());
            return true;
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        try
        {
            await emailSender.SendPasswordResetCodeAsync(account.Email, code, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Member password recovery email is not configured.");
            throw;
        }
        catch (SmtpException exception)
        {
            logger.LogError(exception, "Member password recovery email could not be sent.");
            throw;
        }
        catch (FormatException exception)
        {
            logger.LogError(exception, "The member recovery email address is invalid.");
            throw;
        }

        CacheChallenge(flowToken, account.MemberId, code);
        return true;
    }

    public bool VerifyCode(string flowToken, string code, out int attemptsRemaining)
    {
        lock (ChallengeLock)
        {
            attemptsRemaining = 0;
            if (!cache.TryGetValue(ChallengeKey(flowToken), out Challenge? challenge)
                || challenge is null
                || challenge.ExpiresAt <= DateTimeOffset.UtcNow
                || challenge.Attempts >= MaximumAttempts)
            {
                cache.Remove(ChallengeKey(flowToken));
                return false;
            }

            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(challenge.Code), Encoding.ASCII.GetBytes(code)))
            {
                challenge.Attempts++;
                attemptsRemaining = MaximumAttempts - challenge.Attempts;
                if (attemptsRemaining == 0)
                {
                    cache.Remove(ChallengeKey(flowToken));
                }

                return false;
            }

            challenge.IsVerified = true;
            return true;
        }
    }

    public async Task<bool> ResetPasswordAsync(string flowToken, string newPassword, CancellationToken cancellationToken)
    {
        if (newPassword.Length is < 8 or > 72)
        {
            throw new ArgumentOutOfRangeException(nameof(newPassword));
        }

        if (!cache.TryGetValue(ChallengeKey(flowToken), out Challenge? challenge)
            || challenge is null
            || !challenge.IsVerified
            || string.IsNullOrWhiteSpace(challenge.MemberId)
            || challenge.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            cache.Remove(ChallengeKey(flowToken));
            return false;
        }

        var updated = await repository.ResetMemberPasswordAsync(challenge.MemberId, newPassword, cancellationToken);
        cache.Remove(ChallengeKey(flowToken));
        return updated;
    }

    private void CacheChallenge(string flowToken, string memberId, string code) =>
        cache.Set(ChallengeKey(flowToken), new Challenge(memberId, code, DateTimeOffset.UtcNow.Add(CodeLifetime)), CodeLifetime);

    private static string ChallengeKey(string token) => $"member-password-reset:{token}";

    private static string RateKey(string purpose, string value) =>
        $"member-password-reset-rate:{purpose}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))}";

    private sealed class Challenge(string memberId, string code, DateTimeOffset expiresAt)
    {
        public string MemberId { get; } = memberId;
        public string Code { get; } = code;
        public DateTimeOffset ExpiresAt { get; } = expiresAt;
        public int Attempts { get; set; }
        public bool IsVerified { get; set; }
    }
}
