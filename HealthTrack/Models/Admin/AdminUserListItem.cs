namespace HealthTrack.Models.Admin;

public record AdminUserListItem(string FullName, string? Email, bool IsActive, DateTime CreatedAt,
    List<string> Roles, string Id = "", decimal? TargetWeight = null);
