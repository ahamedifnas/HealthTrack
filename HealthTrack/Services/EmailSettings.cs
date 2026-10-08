namespace HealthTrack.Services;

public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "HealthTrack";
    public bool EnableSsl { get; set; } = true;
    // Required for deployment. Development can use the server's bound loopback URL.
    // Never build recovery links from the request Host header.
    public string PublicBaseUrl { get; set; } = string.Empty;
}
