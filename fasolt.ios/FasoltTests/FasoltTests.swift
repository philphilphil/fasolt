import Testing
@testable import Fasolt

@Suite("Fasolt Tests")
struct FasoltTests {
    @Test("App launches")
    func appExists() {
        #expect(true)
    }
}
