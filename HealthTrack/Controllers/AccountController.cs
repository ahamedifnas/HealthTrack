using HealthTrack.Data;
using HealthTrack.Models;
using HealthTrack.Models.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HealthTrack.Controllers;

[Authorize]
public class AccountController(UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, ApplicationDbContext database) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            FullName = model.FullName.Trim(), Email = model.Email.Trim(), UserName = model.Email.Trim()
        };
        // Keep creation and the Client role assignment atomic.
        await using var transaction = await database.Database.BeginTransactionAsync();
        var result = await users.CreateAsync(user, model.Password);
        if (result.Succeeded)
            result = await users.AddToRoleAsync(user, AppRoles.Client);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await transaction.CommitAsync();
        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
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
            var result = await signIn.PasswordSignInAsync(user, model.Password, model.RememberMe,
                lockoutOnFailure: true);
            if (result.Succeeded)
                return Url.IsLocalUrl(model.ReturnUrl)
                    ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Home");
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
