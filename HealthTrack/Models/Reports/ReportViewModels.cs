namespace HealthTrack.Models.Reports;

public record ReportClient(string Id, string Name, string? Email);
public record ReportMetric(string Label, string Value);
public record ReportTable(string Title, string[] Columns, IReadOnlyList<string[]> Rows);

public class ReportViewModel
{
    public string Title { get; init; } = string.Empty;
    public string? ClientName { get; init; }
    public bool IsAdminReport { get; init; }
    public string Scope { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; } = DateTime.UtcNow;
    public List<ReportMetric> Metrics { get; } = [];
    public List<ReportMetric> PrintMetrics { get; } = [];
    public List<ReportTable> Tables { get; } = [];
}

public enum ClientReportKind { Progress, Workout, Diet, Appointments }
