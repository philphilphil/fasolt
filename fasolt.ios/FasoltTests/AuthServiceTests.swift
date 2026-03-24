import Testing
import Foundation
@testable import Fasolt

@Suite("AuthService PKCE")
struct AuthServicePKCETests {

    @Test("code verifier is 43-128 characters and URL-safe")
    func codeVerifierFormat() {
        let verifier = AuthService.generateCodeVerifier()
        #expect(verifier.count >= 43)
        #expect(verifier.count <= 128)
        let allowed = CharacterSet.alphanumerics.union(CharacterSet(charactersIn: "-._~"))
        #expect(verifier.unicodeScalars.allSatisfy { allowed.contains($0) })
    }

    @Test("code challenge is base64url-encoded SHA256 of verifier")
    func codeChallenge() {
        let verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"
        let challenge = AuthService.generateCodeChallenge(from: verifier)
        #expect(challenge == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM")
    }

    @Test("extracting code from callback URL")
    func extractCode() {
        let url = URL(string: "fasolt://oauth/callback?code=abc123")!
        let code = AuthService.extractCode(from: url)
        #expect(code == "abc123")
    }

    @Test("extracting code returns nil for missing code param")
    func extractCodeMissing() {
        let url = URL(string: "fasolt://oauth/callback?error=access_denied")!
        let code = AuthService.extractCode(from: url)
        #expect(code == nil)
    }
}
