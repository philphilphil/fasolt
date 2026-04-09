import Testing
@testable import Fasolt

@Suite("PasswordRules")
struct PasswordRulesTests {
    @Test("empty password fails all rules")
    func emptyPassword() {
        let rules = PasswordRules.evaluate("")
        #expect(rules.count == 4)
        #expect(rules.allSatisfy { !$0.valid })
    }

    @Test("full valid password passes all rules")
    func fullyValidPassword() {
        let rules = PasswordRules.evaluate("Abcdefg1")
        #expect(rules.allSatisfy { $0.valid })
    }

    @Test("short password fails length rule only")
    func shortPassword() {
        let rules = PasswordRules.evaluate("Abc1")
        #expect(rules.first(where: { $0.label == "At least 8 characters" })?.valid == false)
        #expect(rules.first(where: { $0.label == "Uppercase letter" })?.valid == true)
        #expect(rules.first(where: { $0.label == "Lowercase letter" })?.valid == true)
        #expect(rules.first(where: { $0.label == "Number" })?.valid == true)
    }

    @Test("no uppercase fails only uppercase rule")
    func noUppercase() {
        let rules = PasswordRules.evaluate("abcdefg1")
        #expect(rules.first(where: { $0.label == "Uppercase letter" })?.valid == false)
        #expect(rules.first(where: { $0.label == "At least 8 characters" })?.valid == true)
        #expect(rules.first(where: { $0.label == "Lowercase letter" })?.valid == true)
        #expect(rules.first(where: { $0.label == "Number" })?.valid == true)
    }

    @Test("allValid returns true only when all four pass")
    func allValidHelper() {
        #expect(PasswordRules.allValid("Abcdefg1") == true)
        #expect(PasswordRules.allValid("abcdefg1") == false)
        #expect(PasswordRules.allValid("Abcdefgh") == false)
        #expect(PasswordRules.allValid("") == false)
    }
}
