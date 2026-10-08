using System.Security.Cryptography;
using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

namespace HealthTrack.Services;

public class RegistrationOtpService(ApplicationDbContext database, UserManager<ApplicationUser> users,
    IEmailSender emailSender, TimeProvider time, ILogger<RegistrationOtpService> logger)
{
    public const int ExpiryMinutes = 5;
    public const int MaxAttempts = 5;
    public const int CooldownSeconds = 60;
    private readonly PasswordHasher<EmailVerificationOtp> hasher = new();

    private Task<EmailVerificationOtp?> LockedOtp(string userId) => database.EmailVerificationOtps
        .FromSqlInterpolated($"SELECT * FROM [EmailVerificationOtps] WITH (UPDLOCK, ROWLOCK) WHERE [UserId] = {userId}")
        .SingleOrDefaultAsync();

    public async Task<VerifyEmailOtpViewModel?> GetPageAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || !user.IsActive || !user.RequiresRegistrationEmailVerification || user.EmailConfirmed)
            return null;
        var otp = await database.EmailVerificationOtps.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId);
        if (otp is null) return null;
        return new VerifyEmailOtpViewModel
        {
            ResendSeconds = RemainingCooldown(otp),
            DeliveryFailed = otp.OtpHash.Length == 0
        };
    }

    private int RemainingCooldown(EmailVerificationOtp otp) => otp.LastSentAt is { } last
        ? Math.Max(0, (int)Math.Ceiling((last.AddSeconds(CooldownSeconds) - time.GetUtcNow()).TotalSeconds)) : 0;

    public async Task<string?> SendAsync(string userId)
    {
        string code;
        string email;
        string sentHash;
        // Reserve the resend interval and replace the hash before contacting SMTP.
        // A row update lock serializes resend and verify requests across app instances.
        await using (var transaction = await database.Database.BeginTransactionAsync())
        {
            var otp = await LockedOtp(userId);
            var user = await users.FindByIdAsync(userId);
            if (otp is null || user is null || !user.IsActive || !user.RequiresRegistrationEmailVerification || user.EmailConfirmed)
                return "This verification session is no longer available. Please return to Login.";
            var remaining = RemainingCooldown(otp);
            if (remaining > 0) return $"Please wait {remaining} seconds before requesting another code.";
            // Do not accidentally reissue the immediately previous code.
            do { code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6"); }
            while (otp.OtpHash.Length > 0 && hasher.VerifyHashedPassword(otp, otp.OtpHash, code) != PasswordVerificationResult.Failed);
            var now = time.GetUtcNow();
            otp.OtpHash = hasher.HashPassword(otp, code);
            sentHash = otp.OtpHash;
            otp.CreatedAt = now;
            otp.ExpiresAt = now.AddMinutes(ExpiryMinutes);
            otp.LastSentAt = now;
            otp.AttemptCount = 0;
            otp.IsUsed = false;
            email = user.Email!;
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        try
        {
            await emailSender.SendEmailAsync(email, "Verify your HealthTrack email", RegistrationOtpEmail.CreateHtml(code));
            return null;
        }
        catch (Exception)
        {
            // Provider exceptions can contain message content. Never log them or the OTP.
            logger.LogError("Registration verification email could not be sent. Check email provider availability and configuration.");
            // Invalidate only this failed send, never a newer code or a completed verification.
            await database.EmailVerificationOtps.Where(x => x.UserId == userId && x.OtpHash == sentHash && !x.IsUsed)
                .ExecuteUpdateAsync(updates => updates.SetProperty(x => x.OtpHash, string.Empty).SetProperty(x => x.IsUsed, true));
            return "We couldn't send your verification email. Please wait 60 seconds, then select Resend OTP.";
        }
    }

    public async Task<string?> VerifyAsync(string userId, string code)
    {
        await using var transaction = await database.Database.BeginTransactionAsync();
        var otp = await LockedOtp(userId);
        var user = await users.FindByIdAsync(userId);
        if (otp is null || user is null || !user.IsActive || !user.RequiresRegistrationEmailVerification || user.EmailConfirmed || otp.IsUsed)
            return "This code is no longer available. Request a new code or return to Login.";
        if (otp.AttemptCount >= MaxAttempts)
            return "Too many incorrect attempts. Please request a new code.";
        if (otp.ExpiresAt <= time.GetUtcNow())
            return "Your verification code has expired. Please select Resend OTP.";
        if (hasher.VerifyHashedPassword(otp, otp.OtpHash, code) == PasswordVerificationResult.Failed)
        {
            otp.AttemptCount++;
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
            return otp.AttemptCount >= MaxAttempts ? "Too many incorrect attempts. Please request a new code."
                : $"That code is incorrect. You have {MaxAttempts - otp.AttemptCount} attempts remaining.";
        }

        user.EmailConfirmed = true;
        user.RequiresRegistrationEmailVerification = false;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return "We couldn't complete verification. Please try again.";
        otp.IsUsed = true;
        otp.OtpHash = string.Empty;
        await database.SaveChangesAsync();
        await transaction.CommitAsync();
        return null;
    }
}
