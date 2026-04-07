import Foundation

struct PasswordRule: Equatable, Sendable {
    let label: String
    let valid: Bool
}

enum PasswordRules {
    static func evaluate(_ password: String) -> [PasswordRule] {
        [
            PasswordRule(label: "At least 8 characters", valid: password.count >= 8),
            PasswordRule(label: "Uppercase letter", valid: password.range(of: #"[A-Z]"#, options: .regularExpression) != nil),
            PasswordRule(label: "Lowercase letter", valid: password.range(of: #"[a-z]"#, options: .regularExpression) != nil),
            PasswordRule(label: "Number", valid: password.range(of: #"\d"#, options: .regularExpression) != nil),
        ]
    }

    static func allValid(_ password: String) -> Bool {
        evaluate(password).allSatisfy(\.valid)
    }
}
