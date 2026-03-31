namespace Fasolt.Server.Application.Dtos;

public record UserInfoResponse(string Email, bool IsAdmin, bool EmailConfirmed, string? ExternalProvider = null, string? DisplayName = null);

public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record ConfirmEmailChangeRequest(string NewEmail, string Token);

public record ConfirmEmailRequest(string UserId, string Token);

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password, bool RememberMe = false);

public record DeleteAccountRequest(string? Password, string? ConfirmEmail);
