using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using ITClubOrg.Models;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;

namespace ITClubOrg.Services;

public sealed class PasswordResetService(
    IPasswordResetRepository repository,
    IEmailSender emailSender,
    IMemoryCache cache,
    ILogger<PasswordResetService> logger) : IPasswordResetService
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RequestInterval = TimeSpan.FromMinutes(1);
    private static readonly object ChallengeLock = new();
    private static readonly SemaphoreSlim ResetLock = new(1, 1);
    private const int MaximumAttempts = 3;

    public async Task<bool> SendCodeAsync(
        string flowToken,
        string email,
        string remoteAddress,
        CancellationToken cancellationToken)
    {
        var emailKey = CreateRateLimitKey("email", email.Trim().ToUpperInvariant());
        var addressKey = CreateRateLimitKey("address", remoteAddress);
        if (cache.TryGetValue(emailKey, out _) || cache.TryGetValue(addressKey, out _))
        {
            return false;
        }

        cache.Set(emailKey, true, RequestInterval);
        cache.Set(addressKey, true, RequestInterval);

        PasswordResetAccount? account;
        try
        {
            account = await repository.GetAccountByEmailAsync(email.Trim(), cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Password recovery could not query the configured database.");
            throw;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Password recovery could not query administrator email records.");
            throw;
        }

        if (account is null)
        {
            CacheChallenge(flowToken, 0, RandomNumberGenerator.GetInt32(100000, 1000000).ToString());
            return true;
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        try
        {
            await emailSender.SendPasswordResetCodeAsync(account.Email, code, cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Password recovery email is not configured.");
            CacheChallenge(flowToken, 0, RandomNumberGenerator.GetInt32(100000, 1000000).ToString());
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or OperationCanceledException)
        {
            logger.LogError(exception, "A password recovery email could not be sent.");
            if (exception is OperationCanceledException)
            {
                throw;
            }

            CacheChallenge(flowToken, 0, RandomNumberGenerator.GetInt32(100000, 1000000).ToString());
            return true;
        }
        catch (FormatException exception)
        {
            logger.LogError(exception, "The administrator email address could not be used for password recovery.");
            CacheChallenge(flowToken, 0, RandomNumberGenerator.GetInt32(100000, 1000000).ToString());
            return true;
        }

        CacheChallenge(flowToken, account.AdminId, code);
        return true;
    }

    public bool VerifyCode(string flowToken, string code, out int attemptsRemaining)
    {
        lock (ChallengeLock)
        {
            attemptsRemaining = 0;
            if (!cache.TryGetValue(GetChallengeKey(flowToken), out PasswordResetChallenge? challenge)
                || challenge is null
                || challenge.ExpiresAt <= DateTimeOffset.UtcNow
                || challenge.Attempts >= MaximumAttempts)
            {
                cache.Remove(GetChallengeKey(flowToken));
                return false;
            }

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(challenge.Code),
                    Encoding.ASCII.GetBytes(code)))
            {
                challenge.Attempts++;
                attemptsRemaining = MaximumAttempts - challenge.Attempts;
                if (attemptsRemaining == 0)
                {
                    cache.Remove(GetChallengeKey(flowToken));
                }

                return false;
            }

            challenge.IsVerified = true;
            return true;
        }
    }

    public async Task<bool> ResetPasswordAsync(
        string flowToken,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (newPassword.Length is < 6 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(newPassword), "The password must be between 6 and 64 characters.");
        }

        await ResetLock.WaitAsync(cancellationToken);
        try
        {
            var challengeKey = GetChallengeKey(flowToken);
            if (!cache.TryGetValue(challengeKey, out PasswordResetChallenge? challenge)
                || challenge is null
                || !challenge.IsVerified
                || challenge.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                cache.Remove(challengeKey);
                return false;
            }

            if (!await repository.ResetPasswordAsync(challenge.AdminId, newPassword, cancellationToken))
            {
                cache.Remove(challengeKey);
                return false;
            }

            cache.Remove(challengeKey);
            return true;
        }
        finally
        {
            ResetLock.Release();
        }
    }

    private static string GetChallengeKey(string flowToken) => $"password-reset:{flowToken}";

    private void CacheChallenge(string flowToken, int adminId, string code) =>
        cache.Set(
            GetChallengeKey(flowToken),
            new PasswordResetChallenge(adminId, code, DateTimeOffset.UtcNow.Add(CodeLifetime)),
            CodeLifetime);

    private static string CreateRateLimitKey(string purpose, string value)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        return $"password-reset-rate:{purpose}:{hash}";
    }

    private sealed class PasswordResetChallenge(int adminId, string code, DateTimeOffset expiresAt)
    {
        public int AdminId { get; } = adminId;

        public string Code { get; } = code;

        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public int Attempts { get; set; }

        public bool IsVerified { get; set; }
    }
}
