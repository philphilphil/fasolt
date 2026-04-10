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
public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPasswordResetCodeService _otpService;
    private readonly IOtpEmailSender _emailSender;

    public ForgotPasswordModel(
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
    public string ReturnUrl { get; set; } = "/";

    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool Sent { get; set; }

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";
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
            ErrorMessage = "Please enter a valid email address.";
            return Page();
        }

        // Enumeration guard: look up the user but never reveal whether an
        // account exists. External-provider (GitHub/Apple) and unverified
        // users also fall through to the generic "check your email" view so
        // timing and behaviour can't leak account existence. Same pattern
        // as the post-PR-#110 enumeration-guarded endpoint.
        var user = await _userManager.FindByEmailAsync(Input.Email);
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
                    // Advisory lock race. Still fall through to the generic
                    // confirmation so timing/errors don't leak existence.
                }
            }
        }

        return Redirect($"/oauth/forgot-password?returnUrl={Uri.EscapeDataString(ReturnUrl)}&email={Uri.EscapeDataString(Input.Email)}&sent=true");
    }
}
