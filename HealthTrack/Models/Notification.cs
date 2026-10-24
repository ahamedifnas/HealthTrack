using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models;

public enum NotificationType { Appointment = 1, Workout = 2, Diet = 3, Membership = 4 }

public class Notification
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    [Required, StringLength(150)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Message { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}
