using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;

namespace HealthTrack.Services;

public class SmtpEmailSender(IOptions<EmailSettings> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
            throw new InvalidOperationException("Email delivery is not configured.");
        if (settings.Host.Equals("smtp.gmail.com", StringComparison.OrdinalIgnoreCase)
            && (settings.Port != 587 || !settings.EnableSsl
                || string.IsNullOrWhiteSpace(settings.Username) || string.IsNullOrWhiteSpace(settings.Password)))
            throw new InvalidOperationException("Gmail SMTP requires port 587, TLS and configured authentication.");

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = subject, Body = htmlMessage, IsBodyHtml = true
        };
        message.To.Add(new MailAddress(email));
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrEmpty(settings.Username) ? null
                : new NetworkCredential(settings.Username, settings.Password)
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await client.SendMailAsync(message, timeout.Token);
        // SMTP acceptance is observable without recording recipients, credentials or message contents.
        // Acceptance is not a guarantee of inbox delivery.
        logger.LogInformation(new EventId(1001, "EmailAccepted"), "SMTP server accepted the email message.");
    }
}
