import Foundation

@MainActor
@Observable
final class RegisterViewModel {
    var email = ""
    var password = ""
    var confirmPassword = ""

    var isFormValid: Bool {
        !email.isEmpty
        && email.contains("@")
        && password.count >= 8
        && password.rangeOfCharacter(from: .uppercaseLetters) != nil
        && password.rangeOfCharacter(from: .lowercaseLetters) != nil
        && password.rangeOfCharacter(from: .decimalDigits) != nil
        && password == confirmPassword
    }

    var passwordMismatch: Bool {
        !confirmPassword.isEmpty && password != confirmPassword
    }

    func register(authService: AuthService, serverURL: String) async {
        await authService.register(email: email, password: password, serverURL: serverURL)
    }
}
