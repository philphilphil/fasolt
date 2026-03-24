import SwiftUI

struct SettingsView: View {
    @Environment(AuthService.self) private var authService

    var body: some View {
        NavigationStack {
            List {
                Section {
                    Button("Sign Out", role: .destructive) {
                        authService.signOut()
                    }
                }
            }
            .navigationTitle("Settings")
        }
    }
}
