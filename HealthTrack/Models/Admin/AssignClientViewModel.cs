using System.ComponentModel.DataAnnotations;
namespace HealthTrack.Models.Admin;

public class AssignClientViewModel
{
    [Required, Display(Name = "Client")]
    public string ClientId { get; set; } = string.Empty;
    [Required, Display(Name = "Trainer")]
    public string TrainerId { get; set; } = string.Empty;
}

public record AssignmentListItem(string ClientName, string? ClientEmail, string TrainerName, string? TrainerEmail, DateTime AssignedAt);
