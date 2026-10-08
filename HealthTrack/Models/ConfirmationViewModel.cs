namespace HealthTrack.Models;

public record ConfirmationViewModel(string Title, string Message, string Action, object Id,
    string BackAction, object? BackId = null, string? BackController = null);
