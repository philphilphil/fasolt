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
[EnableRateLimiting("auth-strict")]
[ValidateAntiForgeryToken]
public class RegisterModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IEmailVerificationCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;

    public RegisterModel(
        UserManager<AppUser> userManager,
        IEmailVerificationCodeService otpService,
        IOtpEmailSender emailSender)
    {
        _userManager = userManager;
        _otpService = otpService;
        _emailSender = emailSender;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; } = "/";

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = "";

        public bool TosAccepted { get; set; }
    }

    public IActionResult OnGet()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ReturnUrl = UrlHelpers.IsLocalUrl(ReturnUrl) ? ReturnUrl : "/";

        if (!Input.TosAccepted)
        {
            ErrorMessage = "You must accept the Terms of Service.";
            return Page();
        }

        if (!ModelState.IsValid)
        {
            // Required/EmailAddress validators cover the trivial cases
            ErrorMessage = "Please fill in all fields with a valid email.";
            return Page();
        }

        if (Input.Password != Input.ConfirmPassword)
        {
            ErrorMessage = "Passwords don't match.";
            return Page();
        }

        // Enforce the password policy BEFORE the user lookup. If this ran
        // after FindByEmailAsync, weak passwords + already-taken emails
        // would short-circuit to the verify-email redirect while weak
        // passwords + new emails would error — an enumeration oracle.
        // Running it first makes the error response identical regardless
        // of whether the email exists. Lifted verbatim from the old endpoint.
        var passwordProbe = new AppUser { UserName = Input.Email, Email = Input.Email };
        foreach (var validator in _userManager.PasswordValidators)
        {
            var pwResult = await validator.ValidateAsync(_userManager, passwordProbe, Input.Password);
            if (!pwResult.Succeeded)
            {
                ErrorMessage = string.Join("; ", pwResult.Errors.Select(e => e.Description));
                return Page();
            }
        }

        // Check if email exists. We never reveal whether a given address is
        // already registered (enumeration) and we never let an anonymous
        // POST regenerate OTPs on behalf of an existing user (griefing).
        // Both cases fall through to the same generic verify-email redirect
        // as a fresh signup — indistinguishable from the attacker's side.
        // If the real owner of an unconfirmed account lost their code, they
        // can request a new one from the verify-email page, which is
        // separately rate-limited and goes through CanResendAsync.
        var existing = await _userManager.FindByEmailAsync(Input.Email);
        if (existing is not null)
        {
            return Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(Input.Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
        }

        var user = new AppUser { UserName = Input.Email, Email = Input.Email };
        var createResult = await _userManager.CreateAsync(user, Input.Password);
        if (!createResult.Succeeded)
        {
            // Should be rare now that PasswordValidators ran above, but
            // CreateAsync also enforces User.* policies (e.g. invalid email
            // characters) that we haven't pre-validated.
            ErrorMessage = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Page();
        }

        var code = await _otpService.GenerateAndStoreAsync(user.Id, HttpContext.RequestAborted);
        await _emailSender.SendVerificationCodeAsync(user, user.Email!, code);

        return Redirect($"/oauth/verify-email?email={Uri.EscapeDataString(Input.Email)}&returnUrl={Uri.EscapeDataString(ReturnUrl)}");
    }
}
