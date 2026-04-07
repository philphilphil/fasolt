import Foundation
import os

private let featureLogger = Logger(subsystem: "com.fasolt.app", category: "Features")

@MainActor
@Observable
final class FeatureFlagsService {
    var githubLogin = false
    var appleLogin = false
    var hasLoaded = false

    func refresh(serverURL: String) async {
        guard let url = URL(string: serverURL + "/api/health") else { return }
        do {
            let (data, _) = try await URLSession.shared.data(from: url)
            let decoded = try JSONDecoder().decode(HealthResponse.self, from: data)
            self.githubLogin = decoded.features.githubLogin
            self.appleLogin = decoded.features.appleLogin
            self.hasLoaded = true
            featureLogger.info("Feature flags loaded — github=\(decoded.features.githubLogin) apple=\(decoded.features.appleLogin)")
        } catch {
            featureLogger.error("Failed to load feature flags: \(error.localizedDescription)")
        }
    }

    private struct HealthResponse: Decodable {
        let features: Features
        struct Features: Decodable {
            let githubLogin: Bool
            let appleLogin: Bool
        }
    }
}
