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
public class ResetPasswordModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPasswordResetCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;

    public ResetPasswordModel(
        UserManager<AppUser> userManager,
        IPasswordResetCodeService otpService,
        IOtpEmailSender emailSender)
    {
        _userManager = userManager;
        _otpService = otpService;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string Email { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }
    public bool Success { get; set; }

    public class InputModel
    {
        [Required]
        public string Code { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";
    }

    public IActionResult OnGet(string? error)
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        ErrorMessage = string.IsNullOrEmpty(error) ? null : error;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please fill in all fields.";
            return Page();
        }

        if (Input.Password != Input.ConfirmPassword)
        {
            ErrorMessage = "Passwords don't match.";
            return Page();
        }

        // Run the password policy BEFORE any account lookup so weak-password +
        // unknown-email and weak-password + known-email take identical code
        // paths. Otherwise the policy check on a real user would behave
        // differently from the early-out on an unknown email — an enumeration
        // oracle via latency/errors. Lifted verbatim from the old endpoint.
        var passwordProbe = new AppUser { UserName = Email, Email = Email };
        foreach (var validator in _userManager.PasswordValidators)
        {
            var pwResult = await validator.ValidateAsync(_userManager, passwordProbe, Input.Password);
            if (!pwResult.Succeeded)
            {
                ErrorMessage = string.Join("; ", pwResult.Errors.Select(e => e.Description));
                return Page();
            }
        }

        var user = await _userManager.FindByEmailAsync(Email);
        if (user is null || user.ExternalProvider is not null || !user.EmailConfirmed)
        {
            ErrorMessage = "That code has expired. Request a new one.";
            return Page();
        }

        var verifyResult = await _otpService.VerifyAsync(user.Id, Input.Code, HttpContext.RequestAborted);
        switch (verifyResult)
        {
            case VerifyResult.Ok:
                break;
            case VerifyResult.Incorrect:
                ErrorMessage = "Incorrect code, try again.";
                return Page();
            case VerifyResult.Expired:
            case VerifyResult.NotFound:
                ErrorMessage = "That code has expired. Request a new one.";
                return Page();
            case VerifyResult.LockedOut:
                ErrorMessage = "Too many failed attempts. Try again in 10 minutes.";
                return Page();
            default:
                ErrorMessage = "Something went wrong. Please try again.";
                return Page();
        }

        // OTP consumed. Rotate the password via Remove + Add so SecurityStamp
        // gets bumped and existing cookie sessions eventually invalidate
        // (ValidateSecurityStampAsync interval).
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            ErrorMessage = "Something went wrong. Please try again.";
            return Page();
        }

        var addResult = await _userManager.AddPasswordAsync(user, Input.Password);
        if (!addResult.Succeeded)
        {
            ErrorMessage = string.Join("; ", addResult.Errors.Select(e => e.Description));
            return Page();
        }

        Success = true;
        return Page();
    }

    [EnableRateLimiting("auth-strict")]
    public async Task<IActionResult> OnPostResendAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        // Enumeration guard (same pattern as ForgotPassword): every branch
        // lands on the same generic redirect with no error param, so
        // unknown / external-provider / unverified / cooldown / lockout / ok
        // are all indistinguishable. Rate limiting bounds brute-force spam.
        var user = await _userManager.FindByEmailAsync(Email);
        if (user is not null && user.ExternalProvider is null && user.EmailConfirmed)
        {
            var canResend = await _otpService.CanResendAsync(user.Id, HttpContext.RequestAborted);
            if (canResend == ResendResult.Ok)
            {
                try
                {
                    var code = await _otpService.GenerateAndStoreAsync(user.Id, HttpContext.RequestAborted);
                    await _emailSender.SendPasswordResetCodeAsync(user, user.Email!, code);
                }
                catch (InvalidOperationException)
                {
                    // Advisory lock race — another tab/click won.
                }
            }
        }

        return Redirect($"/oauth/reset-password?email={Uri.EscapeDataString(Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
    }
}
