import Testing
@testable import Fasolt

@Suite("RegisterViewModel")
@MainActor
struct RegisterViewModelTests {
    @Test("form requires email, password, match, and ToS")
    func formRequiresAllGates() {
        let vm = RegisterViewModel()
        #expect(vm.isFormValid == false)

        vm.email = "user@example.com"
        vm.password = "Abcdefg1"
        vm.confirmPassword = "Abcdefg1"
        #expect(vm.isFormValid == false, "ToS not yet accepted")

        vm.tosAccepted = true
        #expect(vm.isFormValid == true)
    }

    @Test("password mismatch blocks submit")
    func passwordMismatchBlocks() {
        let vm = RegisterViewModel()
        vm.email = "user@example.com"
        vm.password = "Abcdefg1"
        vm.confirmPassword = "Abcdefg2"
        vm.tosAccepted = true
        #expect(vm.isFormValid == false)
        #expect(vm.passwordMismatch == true)
    }

    @Test("weak password blocks submit")
    func weakPasswordBlocks() {
        let vm = RegisterViewModel()
        vm.email = "user@example.com"
        vm.password = "abc"
        vm.confirmPassword = "abc"
        vm.tosAccepted = true
        #expect(vm.isFormValid == false)
    }

    @Test("password rules reflect current password")
    func rulesReflectPassword() {
        let vm = RegisterViewModel()
        vm.password = "Abcdefg1"
        #expect(vm.passwordRules.allSatisfy { $0.valid })
    }

    @Test("empty confirm field does not show mismatch")
    func emptyConfirmNoMismatch() {
        let vm = RegisterViewModel()
        vm.password = "Abcdefg1"
        #expect(vm.passwordMismatch == false)
    }
}
