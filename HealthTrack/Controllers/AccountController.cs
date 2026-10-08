using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using HealthTrack.Services;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using System.Text;
using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace HealthTrack.Controllers;

[Authorize]
public class AccountController(UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, ApplicationDbContext database,
    IEmailSender emailSender, IOptions<EmailSettings> emailOptions,
    IWebHostEnvironment environment, IServer server, ILogger<AccountController> logger,
    RegistrationOtpService registrationOtp, IDataProtectionProvider protection) : Controller
{
    private const string InvalidReset = "This password reset link is invalid or has expired. Please request a new link.";

    [AllowAnonymous, HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is not null && user.IsActive)
        {
            try
            {
                var origin = emailOptions.Value.PublicBaseUrl;
                // Local demos work with either launch profile without trusting the request Host header.
                // Deployed instances must explicitly configure their public HTTPS URL.
                if (string.IsNullOrWhiteSpace(origin) && environment.IsDevelopment())
                    origin = server.Features.Get<IServerAddressesFeature>()?.Addresses
                        .Where(address => Uri.TryCreate(address, UriKind.Absolute, out var bound)
                            && bound.IsLoopback && bound.Scheme is "http" or "https")
                        .OrderByDescending(address => address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault() ?? string.Empty;
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                    || (uri.Scheme != "https" && !(environment.IsDevelopment() && uri.Scheme == "http" && uri.IsLoopback))
                    || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                    throw new InvalidOperationException("A trusted email public URL is required.");
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var link = QueryHelpers.AddQueryString(origin.TrimEnd('/') + "/Account/ResetPassword",
                    new Dictionary<string, string?> { ["email"] = user.Email, ["token"] = encoded });
                await emailSender.SendEmailAsync(user.Email!, "Reset your HealthTrack password",
                    PasswordResetEmail.CreateHtml(link));
            }
            catch (Exception exception)
            {
                // Do not log exception payloads: providers can include recipients or message content.
                var category = exception switch
                {
                    SmtpException smtp => $"SMTP status {(int)smtp.StatusCode}",
                    OperationCanceledException => "Delivery timeout",
                    InvalidOperationException or ArgumentException => "Email configuration",
                    _ => "Delivery failure"
                };
                logger.LogError("Password recovery email could not be delivered ({Category}). Check email configuration and provider availability.", category);
            }
        }
        // Identical response for unknown, inactive and known accounts, including delivery failures.
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [AllowAnonymous, HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !TryDecodeToken(token, out _))
            ModelState.AddModelError(string.Empty, InvalidReset);
        return View(new ResetPasswordViewModel { Email = email ?? string.Empty, Token = token ?? string.Empty });
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!TryDecodeToken(model.Token, out var token))
            ModelState.AddModelError(string.Empty, InvalidReset);
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, InvalidReset);
            return View(model);
        }
        var result = await users.ResetPasswordAsync(user, token, model.Password);
        if (result.Succeeded) return RedirectToAction(nameof(ResetPasswordConfirmation));
        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Code == "InvalidToken" ? InvalidReset : error.Description);
        return View(model);
    }

    [AllowAnonymous, HttpGet]
    public IActionResult ResetPasswordConfirmation() => View();

    private static bool TryDecodeToken(string? encoded, out string token)
    {
        token = string.Empty;
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 4096) return false;
        try
        {
            token = new UTF8Encoding(false, true).GetString(WebEncoders.Base64UrlDecode(encoded));
            return token.Length > 0;
        }
        catch (Exception exception) when (exception is FormatException or DecoderFallbackException)
        {
            return false;
        }
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            FullName = model.FullName.Trim(), Email = model.Email.Trim(), UserName = model.Email.Trim(),
            RequiresRegistrationEmailVerification = true
        };
        // Keep creation and the Client role assignment atomic.
        await using (var transaction = await database.Database.BeginTransactionAsync())
        {
            IdentityResult result;
            try
            {
                result = await users.CreateAsync(user, model.Password);
                if (result.Succeeded)
                    result = await users.AddToRoleAsync(user, AppRoles.Client);
            }
            catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
            {
                ModelState.AddModelError(nameof(model.Email), "An account with this email is already taken.");
                return View(model);
            }

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            database.EmailVerificationOtps.Add(new EmailVerificationOtp { UserId = user.Id, IsUsed = true });
            await database.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        SetPendingRegistration(user.Id);
        var deliveryError = await registrationOtp.SendAsync(user.Id);
        if (deliveryError is not null) TempData["OtpError"] = deliveryError;
        return RedirectToAction(nameof(VerifyEmailOtp));
    }

    private const string PendingRegistrationCookie = "HealthTrack.Registration";

    private void SetPendingRegistration(string userId) => Response.Cookies.Append(PendingRegistrationCookie,
        protection.CreateProtector("HealthTrack.Registration.v1").ToTimeLimitedDataProtector().Protect(userId, TimeSpan.FromDays(1)),
        new CookieOptions { HttpOnly = true, Secure = Request.IsHttps, SameSite = SameSiteMode.Lax,
            IsEssential = true, MaxAge = TimeSpan.FromDays(1), Path = "/" });

    private string? PendingRegistration()
    {
        if (!Request.Cookies.TryGetValue(PendingRegistrationCookie, out var value)) return null;
        try { return protection.CreateProtector("HealthTrack.Registration.v1").ToTimeLimitedDataProtector().Unprotect(value); }
        catch (CryptographicException) { return null; }
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> VerifyEmailOtp()
    {
        var userId = PendingRegistration();
        var model = userId is null ? null : await registrationOtp.GetPageAsync(userId);
        return model is null ? RedirectToAction(nameof(Login)) : View(model);
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmailOtp(VerifyEmailOtpViewModel model)
    {
        var userId = PendingRegistration();
        if (userId is null) return RedirectToAction(nameof(Login));
        var error = ModelState.IsValid ? await registrationOtp.VerifyAsync(userId, model.Code)
            : "Enter exactly 6 digits.";
        if (error is null)
        {
            Response.Cookies.Delete(PendingRegistrationCookie, new CookieOptions { Path = "/" });
            TempData["RegistrationVerified"] = true;
            return RedirectToAction(nameof(Login));
        }
        // Never echo the submitted OTP into rendered HTML, including validation responses.
        ModelState.Clear();
        ModelState.AddModelError(nameof(model.Code), error);
        var page = await registrationOtp.GetPageAsync(userId);
        return page is null ? RedirectToAction(nameof(Login)) : View(page);
    }

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendEmailOtp()
    {
        var userId = PendingRegistration();
        if (userId is null) return RedirectToAction(nameof(Login));
        var error = await registrationOtp.SendAsync(userId);
        TempData[error is null ? "OtpSuccess" : "OtpError"] = error ?? "A new verification code has been sent. Use the latest email.";
        return RedirectToAction(nameof(VerifyEmailOtp));
    }

    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await users.FindByEmailAsync(model.Email.Trim());
        if (user is not null && user.IsActive)
        {
            if (user.RequiresRegistrationEmailVerification && !user.EmailConfirmed)
            {
                // Check the password and preserve lockout before revealing the pending verification state.
                var check = await signIn.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
                if (check.Succeeded)
                {
                    SetPendingRegistration(user.Id);
                    ViewData["PendingEmailVerification"] = true;
                    ModelState.AddModelError(string.Empty, "Please verify your email address before signing in.");
                    return View(model);
                }
            }
            else
            {
                var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe,
                    lockoutOnFailure: true);
                if (result.Succeeded)
                    return RedirectToAction("Index", "Dashboard");
            }
        }

        ModelState.AddModelError(string.Empty, "Unable to sign in. Check your credentials or try again later.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
