using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

// Kept independently of the notification so deleting a message cannot generate it again.
public class ReminderDelivery
{
    public string UserId { get; set; } = string.Empty;
    [MaxLength(120)] public string ReminderKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
