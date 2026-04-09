using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Fasolt.Server.Api.Helpers;
using Fasolt.Server.Application.Auth;
using Fasolt.Server.Domain.Entities;

namespace Fasolt.Server.Pages.Oauth;

[AllowAnonymous]
[EnableRateLimiting("auth")]
[ValidateAntiForgeryToken]
public class LoginModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IEmailVerificationCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public LoginModel(
        SignInManager<AppUser> signInManager,
        IEmailVerificationCodeService otpService,
        IOtpEmailSender emailSender,
        IConfiguration configuration)
    {
        _signInManager = signInManager;
        _otpService = otpService;
        _emailSender = emailSender;
        _configuration = configuration;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }

    public bool GitHubEnabled => !string.IsNullOrEmpty(_configuration["GITHUB_CLIENT_ID"]);

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public IActionResult OnGet([FromQuery(Name = "provider_hint")] string? providerHint, string? error)
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        // Provider-hint redirect: if the caller passes ?provider_hint=github
        // and GitHub is configured, bounce straight into the GitHub OAuth
        // flow without rendering the login page. iOS relies on this to
        // short-circuit to GitHub when the user has previously used it.
        if (providerHint == "github" && GitHubEnabled)
        {
            return Redirect($"/api/account/github-login?returnUrl={Uri.EscapeDataString(ReturnUrl)}");
        }

        // Friendly error mapping for GitHub OAuth callback failures. Matches
        // the map in the old SPA LoginView.vue so UX doesn't change.
        ErrorMessage = error switch
        {
            "github_auth_failed" => "GitHub authentication failed. Please try again.",
            "account_creation_failed" => "Could not create your account. Please try again.",
            null or "" => null,
            _ => error,
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (!ModelState.IsValid)
        {
            // Field-level errors (missing required, bad email format) render
            // via the asp-validation-for tag helpers in the template.
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password,
            isPersistent: true, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            ErrorMessage = result.IsLockedOut
                ? "Account locked. Try again later."
                : "Invalid email or password.";
            return Page();
        }

        var user = await _signInManager.UserManager.FindByEmailAsync(Input.Email);
        if (user is null)
        {
            // Racy corner case: signed in moments ago, gone now. Bail cleanly.
            ErrorMessage = "Invalid email or password.";
            return Page();
        }

        // Unverified accounts must complete OTP verification before we hand
        // out a persistent cookie. Same logic as the old endpoint: sign the
        // user out of the PasswordSignIn, generate a fresh OTP (respecting
        // the resend window), and send them to the verify page.
        if (!user.EmailConfirmed)
        {
            await _signInManager.SignOutAsync();

            var canResend = await _otpService.CanResendAsync(user.Id, HttpContext.RequestAborted);
            if (canResend == ResendResult.Ok)
            {
                try
                {
                    var code = await _otpService.GenerateAndStoreAsync(user.Id, HttpContext.RequestAborted);
                    await _emailSender.SendVerificationCodeAsync(user, user.Email!, code);
                }
                catch (InvalidOperationException)
                {
                    // Race against the advisory lock — another tab/click won.
                    // Fall through to the verify page silently.
                }
            }

            return Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(Input.Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
        }

        return Redirect(ReturnUrl);
    }
}
