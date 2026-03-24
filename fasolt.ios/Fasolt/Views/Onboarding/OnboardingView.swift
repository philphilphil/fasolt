import SwiftUI

struct OnboardingView: View {
    @Environment(AuthService.self) private var authService
    @State private var showServerField = false
    @State private var serverURL = AuthService.defaultServerURL

    var body: some View {
        VStack(spacing: 32) {
            Spacer()

            VStack(spacing: 8) {
                Image(systemName: "rectangle.on.rectangle.angled")
                    .font(.system(size: 64))
                    .foregroundStyle(.tint)
                Text("Fasolt")
                    .font(.largeTitle.bold())
                Text("Spaced repetition for your notes")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }

            Spacer()

            if showServerField {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Server URL")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    TextField("https://fasolt.app", text: $serverURL)
                        .textFieldStyle(.roundedBorder)
                        .textContentType(.URL)
                        .autocorrectionDisabled()
                        .textInputAutocapitalization(.never)
                        .keyboardType(.URL)
                }
                .padding(.horizontal)
                .transition(.move(edge: .bottom).combined(with: .opacity))
            }

            Button {
                Task {
                    await authService.signIn(serverURL: serverURL)
                }
            } label: {
                if authService.isLoading {
                    ProgressView()
                        .frame(maxWidth: .infinity)
                        .frame(height: 22)
                } else {
                    Text("Sign In")
                        .frame(maxWidth: .infinity)
                }
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(authService.isLoading || serverURL.isEmpty)
            .padding(.horizontal)

            if let error = authService.errorMessage {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal)
            }

            if !showServerField {
                Button("Self-hosting? Change server") {
                    withAnimation {
                        showServerField = true
                    }
                }
                .font(.caption)
                .foregroundStyle(.secondary)
            }

            Spacer()
                .frame(height: 40)
        }
    }
}

#Preview {
    OnboardingView()
        .environment(AuthService())
}
