import SwiftUI

struct OfflineBanner: ViewModifier {
    @Environment(NetworkMonitor.self) private var networkMonitor
    @Environment(BackendStatusMonitor.self) private var backendStatus

    private var shouldShow: Bool {
        !networkMonitor.isConnected || !backendStatus.isReachable
    }

    private var icon: String {
        networkMonitor.isConnected ? "exclamationmark.icloud" : "wifi.slash"
    }

    private var label: String {
        networkMonitor.isConnected ? "Can't reach server" : "Offline"
    }

    func body(content: Content) -> some View {
        VStack(spacing: 0) {
            if shouldShow {
                HStack(spacing: 6) {
                    Image(systemName: icon)
                        .font(.caption2)
                    Text(label)
                        .font(.caption2.weight(.medium))
                }
                .foregroundStyle(.secondary)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 6)
                .background(.ultraThinMaterial)
                .transition(.move(edge: .top).combined(with: .opacity))
            }

            content
        }
        .animation(.easeInOut(duration: 0.3), value: shouldShow)
    }
}

extension View {
    func offlineBanner() -> some View {
        modifier(OfflineBanner())
    }
}
