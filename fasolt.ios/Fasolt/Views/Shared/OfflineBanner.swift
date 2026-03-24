import SwiftUI

struct OfflineBanner: ViewModifier {
    @Environment(NetworkMonitor.self) private var networkMonitor

    func body(content: Content) -> some View {
        VStack(spacing: 0) {
            if !networkMonitor.isConnected {
                HStack(spacing: 6) {
                    Image(systemName: "wifi.slash")
                        .font(.caption2)
                    Text("Offline")
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
        .animation(.easeInOut(duration: 0.3), value: networkMonitor.isConnected)
    }
}

extension View {
    func offlineBanner() -> some View {
        modifier(OfflineBanner())
    }
}
