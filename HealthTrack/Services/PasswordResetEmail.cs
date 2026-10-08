using System.Text.Encodings.Web;

namespace HealthTrack.Services;

public static class PasswordResetEmail
{
    public static string CreateHtml(string resetUrl)
    {
        var safeUrl = HtmlEncoder.Default.Encode(resetUrl);
        // Inline styling and a presentation table keep the email readable in common mail clients.
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"><title>Reset your HealthTrack password</title></head>
            <body style="margin:0;padding:24px 12px;background:#f3f7f5;color:#203a36;font-family:Arial,Helvetica,sans-serif;line-height:1.6;">
              <table role="presentation" style="width:100%;max-width:520px;margin:0 auto;border-collapse:separate;border-spacing:0;">
                <tr><td style="padding:0 0 20px;text-align:center;color:#126b60;font-size:24px;font-weight:bold;">+ HealthTrack</td></tr>
                <tr><td style="padding:32px 24px;background:#ffffff;border:1px solid #dce7e2;border-radius:12px;">
                  <h1 style="margin:0 0 16px;font-size:24px;line-height:1.3;">Reset your password</h1>
                  <p>We received a request to reset your HealthTrack password.</p>
                  <p style="margin:28px 0;"><a href="{{safeUrl}}" style="display:inline-block;padding:12px 24px;background:#126b60;border-radius:6px;color:#ffffff;text-decoration:none;font-weight:bold;">Reset Password</a></p>
                  <p>This link is valid for a limited time and can be used only once.</p>
                  <p>If you did not request this reset, you can safely ignore this email.</p>
                  <p>For your security, this reset link should only be used by you.</p>
                  <p style="margin-bottom:0;">Regards,<br>HealthTrack Team</p>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }
}
