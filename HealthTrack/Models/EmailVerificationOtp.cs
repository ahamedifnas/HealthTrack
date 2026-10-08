using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public class EmailVerificationOtp
{
    [Key]
    public string UserId { get; set; } = string.Empty;
    [MaxLength(512)]
    public string OtpHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LastSentAt { get; set; }
    public bool IsUsed { get; set; }
    public int AttemptCount { get; set; }
}
