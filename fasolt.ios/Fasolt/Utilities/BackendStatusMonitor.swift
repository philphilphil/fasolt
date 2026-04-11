import Foundation

/// Tracks whether the backend is reachable based on the outcome of recent API
/// requests. Unlike `NetworkMonitor` (device-level connectivity), this reflects
/// whether we can actually talk to the server — it flips to unreachable on
/// network errors or 5xx responses and back on any successful response.
///
/// Updated passively via `NotificationCenter`; no polling or health pings.
@MainActor
@Observable
final class BackendStatusMonitor {
    var isReachable: Bool = true

    @ObservationIgnored nonisolated(unsafe) private var reachableObserver: Any?
    @ObservationIgnored nonisolated(unsafe) private var unreachableObserver: Any?

    init() {
        reachableObserver = NotificationCenter.default.addObserver(
            forName: .backendDidBecomeReachable,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self else { return }
                if !self.isReachable { self.isReachable = true }
            }
        }
        unreachableObserver = NotificationCenter.default.addObserver(
            forName: .backendDidBecomeUnreachable,
            object: nil,
            queue: .main
        ) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self else { return }
                if self.isReachable { self.isReachable = false }
            }
        }
    }

    deinit {
        if let reachableObserver { NotificationCenter.default.removeObserver(reachableObserver) }
        if let unreachableObserver { NotificationCenter.default.removeObserver(unreachableObserver) }
    }
}
