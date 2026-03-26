import SwiftUI

struct OnboardingView: View {
    @Environment(AuthService.self) private var authService
    @State private var showServerField = false
    @State private var serverURL = AuthService.defaultServerURL
    @State private var showRegistrationSuccess = false
    private static let selfHostDefault = "http://localhost:8080"

    var body: some View {
        NavigationStack {
            VStack(spacing: 32) {
                Spacer()

                VStack(spacing: 8) {
                    Image("FasoltLogo")
                        .resizable()
                        .aspectRatio(contentMode: .fit)
                        .frame(width: 120, height: 120)
                        .clipShape(RoundedRectangle(cornerRadius: 28, style: .continuous))
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

                VStack(spacing: 12) {
                    NavigationLink {
                        RegisterView(serverURL: serverURL)
                    } label: {
                        Text("Create Account")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .disabled(serverURL.isEmpty)

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
                    .buttonStyle(.bordered)
                    .controlSize(.large)
                    .disabled(authService.isLoading || serverURL.isEmpty)
                }
                .padding(.horizontal)

                if showRegistrationSuccess {
                    Text("Account created! Sign in to get started.")
                        .font(.caption)
                        .foregroundStyle(.green)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)
                }

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
                            serverURL = Self.selfHostDefault
                            showServerField = true
                        }
                    }
                    .font(.caption)
                    .foregroundStyle(.secondary)
                }

                Spacer()
                    .frame(height: 40)
            }
            .onChange(of: authService.registrationSuccess) { _, success in
                if success {
                    showRegistrationSuccess = true
                    authService.registrationSuccess = false
                }
            }
        }
    }
}

#Preview {
    OnboardingView()
        .environment(AuthService())
}
