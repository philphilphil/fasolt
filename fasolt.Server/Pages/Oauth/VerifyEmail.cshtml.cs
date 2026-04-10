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
[EnableRateLimiting("auth")] // OnPostResendAsync overrides this with "auth-strict"
[ValidateAntiForgeryToken]
public class VerifyEmailModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly IEmailVerificationCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;

    public VerifyEmailModel(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        IEmailVerificationCodeService otpService,
        IOtpEmailSender emailSender)
    {
        _userManager = userManager;
        _signInManager = signInManager;
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

    public class InputModel
    {
        [Required]
        public string Code { get; set; } = "";
    }

    public IActionResult OnGet()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Please enter the code.";
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Email);
        if (user is null)
        {
            ErrorMessage = "That code has expired. Request a new one.";
            return Page();
        }

        var result = await _otpService.VerifyAsync(user.Id, Input.Code, HttpContext.RequestAborted);
        switch (result)
        {
            case VerifyResult.Ok:
                // UpdateAsync must precede SignInAsync so the Identity cookie
                // reflects the confirmed state. If UpdateAsync silently failed,
                // we'd hand out a cookie claiming "email_confirmed" while the
                // DB row still said otherwise — a confusing half-state, and
                // the OTP row has already been consumed inside VerifyAsync.
                user.EmailConfirmed = true;
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    ErrorMessage = "Something went wrong. Please try again.";
                    return Page();
                }

                // isPersistent: true matches the convergence decision (Task 4
                // for /oauth/login); auth pages default to persistent sessions
                // per the modern SaaS norm.
                await _signInManager.SignInAsync(user, isPersistent: true);
                return Redirect(ReturnUrl);

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
    }

    [EnableRateLimiting("auth-strict")]
    public async Task<IActionResult> OnPostResendAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        // Resend behavior lifted verbatim from the old endpoint. Unlike the
        // password-reset resend (which is fully enumeration-guarded since
        // PR #110), verify-email resend still surfaces specific errors for
        // known throttled users while unknown users fall through to a clean
        // redirect. This is a small enumeration oracle but low priority —
        // reaching this flow requires having already gone through register,
        // where the email was already knowable. Tracked as a follow-up if
        // it ever matters.
        var user = await _userManager.FindByEmailAsync(Email);
        if (user is null)
        {
            return Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
        }

        var canResend = await _otpService.CanResendAsync(user.Id, HttpContext.RequestAborted);
        switch (canResend)
        {
            case ResendResult.TooSoon:
                ErrorMessage = "Please wait before requesting another code.";
                return Page();
            case ResendResult.LockedOut:
                ErrorMessage = "Too many failed attempts. Try again in 10 minutes.";
                return Page();
            case ResendResult.TooManyAttempts:
                ErrorMessage = "Too many codes sent. Please wait and try again later.";
                return Page();
        }

        // CanResendAsync above is advisory; GenerateAndStoreAsync re-checks
        // cap/cooldown inside an advisory lock. If another request won the
        // race, translate the throw into the same user-visible "too soon"
        // error rather than a 500.
        try
        {
            var code = await _otpService.GenerateAndStoreAsync(user.Id, HttpContext.RequestAborted);
            await _emailSender.SendVerificationCodeAsync(user, user.Email!, code);
        }
        catch (InvalidOperationException)
        {
            ErrorMessage = "Please wait before requesting another code.";
            return Page();
        }

        return Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
    }
}
