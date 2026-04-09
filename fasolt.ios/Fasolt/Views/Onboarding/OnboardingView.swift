import SwiftUI
import AuthenticationServices

struct OnboardingView: View {
    @Environment(AuthService.self) private var authService
    @Environment(FeatureFlagsService.self) private var featureFlags
    @State private var showServerField = false
    @State private var serverURL = AuthService.defaultServerURL
    private static let selfHostDefault = "http://localhost:8080"

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: 28) {
                    Spacer().frame(height: 40)

                    VStack(spacing: 8) {
                        Image("FasoltLogo")
                            .resizable()
                            .aspectRatio(contentMode: .fit)
                            .frame(width: 96, height: 96)
                            .clipShape(RoundedRectangle(cornerRadius: 22, style: .continuous))
                        Text("Fasolt")
                            .font(.largeTitle.bold())
                        Text("Spaced repetition for your notes")
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                    }

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
                                .onChange(of: serverURL) { _, newValue in
                                    Task { await featureFlags.refresh(serverURL: newValue) }
                                }
                        }
                        .padding(.horizontal)
                        .transition(.move(edge: .bottom).combined(with: .opacity))
                    }

                    // SSO providers — always first-class
                    VStack(spacing: 10) {
                        if featureFlags.appleLogin {
                            SignInWithAppleButton(
                                .continue,
                                onRequest: { request in
                                    request.requestedScopes = [.fullName, .email]
                                },
                                onCompletion: { result in
                                    handleAppleResult(result)
                                }
                            )
                            .signInWithAppleButtonStyle(.black)
                            .frame(height: 48)
                            .cornerRadius(8)
                        }

                        if featureFlags.githubLogin {
                            Button {
                                Task {
                                    await authService.signIn(serverURL: serverURL, providerHint: "github")
                                }
                            } label: {
                                HStack {
                                    Image(systemName: "chevron.left.forwardslash.chevron.right")
                                    Text("Continue with GitHub")
                                        .fontWeight(.medium)
                                }
                                .frame(maxWidth: .infinity)
                                .frame(height: 48)
                                .background(Color(red: 36/255, green: 41/255, blue: 47/255))
                                .foregroundStyle(.white)
                                .cornerRadius(8)
                            }
                        }
                    }
                    .padding(.horizontal)

                    if featureFlags.appleLogin || featureFlags.githubLogin {
                        HStack {
                            VStack { Divider() }
                            Text("or")
                                .font(.caption)
                                .foregroundStyle(.secondary)
                            VStack { Divider() }
                        }
                        .padding(.horizontal)
                    }

                    // Email — single button that opens the web popup
                    Button {
                        Task {
                            await authService.signIn(serverURL: serverURL, screenHint: "signup")
                        }
                    } label: {
                        if authService.isLoading {
                            ProgressView()
                                .frame(maxWidth: .infinity)
                                .frame(height: 22)
                        } else {
                            Text("Continue with email")
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
                                serverURL = Self.selfHostDefault
                                showServerField = true
                            }
                            Task { await featureFlags.refresh(serverURL: Self.selfHostDefault) }
                        }
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    }

                    Spacer().frame(height: 32)
                }
            }
        }
    }

    private func handleAppleResult(_ result: Result<ASAuthorization, Error>) {
        switch result {
        case .success(let authorization):
            guard let credential = authorization.credential as? ASAuthorizationAppleIDCredential,
                  let tokenData = credential.identityToken,
                  let identityToken = String(data: tokenData, encoding: .utf8) else {
                authService.errorMessage = "Could not read Apple credential."
                return
            }
            Task {
                await authService.signInWithApple(identityToken: identityToken, serverURL: serverURL)
            }
        case .failure(let error):
            if (error as NSError).code == ASAuthorizationError.canceled.rawValue {
                return
            }
            authService.errorMessage = "Apple sign-in failed: \(error.localizedDescription)"
        }
    }
}

#Preview {
    OnboardingView()
        .environment(AuthService())
        .environment(FeatureFlagsService())
}
