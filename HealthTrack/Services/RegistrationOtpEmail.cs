using System.Text.Encodings.Web;

namespace HealthTrack.Services;

public static class RegistrationOtpEmail
{
    public static string CreateHtml(string code) => $$"""
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1.0"><title>Verify your email</title></head>
        <body style="margin:0;padding:24px 12px;background:#f3f7f5;color:#203a36;font-family:Arial,Helvetica,sans-serif;line-height:1.6;">
        <table role="presentation" style="width:100%;max-width:520px;margin:0 auto;border-spacing:0;">
        <tr><td style="padding-bottom:20px;text-align:center;color:#126b60;font-size:24px;font-weight:bold;">+ HealthTrack</td></tr>
        <tr><td style="padding:32px 24px;background:white;border:1px solid #dce7e2;border-radius:12px;">
        <h1 style="font-size:24px;margin-top:0;">Verify your email</h1>
        <p>Your verification code is:</p>
        <p style="padding:16px;background:#e7f3ef;border-radius:8px;font-size:32px;font-weight:bold;letter-spacing:6px;text-align:center;color:#0c5149;">{{HtmlEncoder.Default.Encode(code)}}</p>
        <p>This code will expire in 5 minutes. Do not share it with anyone.</p>
        <p>If you did not create a HealthTrack account, you can ignore this email.</p>
        <p>Regards,<br>HealthTrack Team</p>
        </td></tr></table></body></html>
        """;
}
