namespace HealthTrack.Models.Dashboard;

public record DashboardMetric(string Title, string Value, string? Hint = null, string? TestId = null);
