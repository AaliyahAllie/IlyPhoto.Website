using System.Security.Claims;
using IlyPhotoWeb.Models;
using IlyPhotoWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace IlyPhotoWeb.Controllers;

public class AccountController : Controller
{
    private readonly AuthService _auth;
    public AccountController(AuthService auth) => _auth = auth;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToDashboard(User.IsInRole(Roles.Admin));

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.SignInAsync(model.Email.Trim().ToLowerInvariant(), model.Password);
        if (!result.Ok)
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }

        await SignInCookieAsync(result.Profile!);
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return LocalRedirect(model.ReturnUrl);

        return RedirectToDashboard(result.Profile!.Role == Roles.Admin);
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToDashboard(User.IsInRole(Roles.Admin));

        return View(new RegisterViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.RegisterAsync(
            model.FullName.Trim(),
            model.Email.Trim().ToLowerInvariant(),
            model.Phone?.Trim() ?? "",
            model.Password);

        if (!result.Ok)
        {
            ModelState.AddModelError("", result.Error!);
            return View(model);
        }

        await SignInCookieAsync(result.Profile!);
        return RedirectToAction("Index", "Client");
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToDashboard(User.IsInRole(Roles.Admin));

        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.ResetPasswordAsync(model.Email.Trim().ToLowerInvariant(), model.NewPassword);
        if (!result.Ok)
        {
            ModelState.AddModelError("", result.Error ?? "Password recovery failed. Please verify your registered email address.");
            return View(model);
        }

        TempData["LoginSuccess"] = "Your password has been reset and a confirmation notification was sent to your account. You may now sign in.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private IActionResult RedirectToDashboard(bool isAdmin) =>
        RedirectToAction("Index", isAdmin ? "Admin" : "Client");

    private Task SignInCookieAsync(UserProfile p)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, p.Id),
            new(ClaimTypes.Email, p.Email),
            new(ClaimTypes.Name, p.FullName),
            new(ClaimTypes.Role, p.Role),
            new("ClientId", p.ClientId)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
