namespace Fasolt.Server.Application.Dtos;

public record UserInfoResponse(string Email, bool IsAdmin, string? ExternalProvider = null);

public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);

public record ConfirmEmailChangeRequest(string NewEmail, string Token);
