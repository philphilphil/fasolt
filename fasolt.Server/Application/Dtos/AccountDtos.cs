namespace Fasolt.Server.Application.Dtos;

public record UserInfoResponse(string Email, bool IsAdmin, bool EmailConfirmed, string? ExternalProvider = null, string? DisplayName = null);

public record ChangeEmailRequest(string NewEmail, string CurrentPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ConfirmEmailChangeRequest(string NewEmail, string Token);

public record DeleteAccountRequest(string? Password, string? ConfirmIdentity);
