import Foundation

@MainActor
@Observable
final class RegisterViewModel {
    var email = ""
    var password = ""
    var confirmPassword = ""
    var tosAccepted = false

    var passwordRules: [PasswordRule] {
        PasswordRules.evaluate(password)
    }

    var passwordsMatch: Bool {
        password == confirmPassword
    }

    var passwordMismatch: Bool {
        !confirmPassword.isEmpty && !passwordsMatch
    }

    var isFormValid: Bool {
        !email.isEmpty
            && email.contains("@")
            && PasswordRules.allValid(password)
            && passwordsMatch
            && tosAccepted
    }

    func register(authService: AuthService, serverURL: String) async {
        await authService.register(email: email, password: password, serverURL: serverURL)
    }
}
