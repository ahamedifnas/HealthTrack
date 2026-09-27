namespace HealthTrack.Models.Training;

// Assignments are maintained separately from plans so an empty client can be assigned.
public class TrainerClient
{
    public string TrainerId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser Trainer { get; set; } = null!;
    public ApplicationUser Client { get; set; } = null!;
}
