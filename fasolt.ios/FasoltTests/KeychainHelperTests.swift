import Foundation
import Testing
@testable import Fasolt

@Suite("KeychainHelper")
struct KeychainHelperTests {
    let keychain = KeychainHelper()
    let testKey = "test.keychain.key.\(UUID().uuidString)"

    @Test("save and retrieve a string value")
    func saveAndRetrieve() {
        keychain.save("hello", forKey: testKey)
        let result = keychain.retrieve(testKey)
        #expect(result == "hello")
        keychain.delete(testKey)
    }

    @Test("overwrite existing value")
    func overwrite() {
        keychain.save("first", forKey: testKey)
        keychain.save("second", forKey: testKey)
        let result = keychain.retrieve(testKey)
        #expect(result == "second")
        keychain.delete(testKey)
    }

    @Test("retrieve returns nil for missing key")
    func missingKey() {
        let result = keychain.retrieve("nonexistent.key.\(UUID().uuidString)")
        #expect(result == nil)
    }

    @Test("delete removes value")
    func deleteValue() {
        keychain.save("toDelete", forKey: testKey)
        keychain.delete(testKey)
        let result = keychain.retrieve(testKey)
        #expect(result == nil)
    }

    @Test("deleteAll clears all fasolt keys")
    func deleteAll() {
        let key1 = "fasolt.test1.\(UUID().uuidString)"
        let key2 = "fasolt.test2.\(UUID().uuidString)"
        keychain.save("a", forKey: key1)
        keychain.save("b", forKey: key2)
        keychain.deleteAll()
        #expect(keychain.retrieve(key1) == nil)
        #expect(keychain.retrieve(key2) == nil)
    }
}
