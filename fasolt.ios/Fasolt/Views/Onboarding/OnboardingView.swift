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
                            .foregroundStyle(FasoltTheme.ink0)
                        Text("Spaced repetition for your notes")
                            .font(.subheadline)
                            .foregroundStyle(FasoltTheme.ink2)
                    }

                    if showServerField {
                        VStack(alignment: .leading, spacing: 6) {
                            Text("Server URL")
                                .font(.system(size: 13, weight: .medium))
                                .foregroundStyle(FasoltTheme.ink2)
                            TextField("https://fasolt.app", text: $serverURL)
                                .textFieldStyle(.plain)
                                .font(.system(size: 16))
                                .foregroundStyle(FasoltTheme.ink0)
                                .textContentType(.URL)
                                .autocorrectionDisabled()
                                .textInputAutocapitalization(.never)
                                .keyboardType(.URL)
                                .padding(.horizontal, 14)
                                .padding(.vertical, 12)
                                .background(
                                    RoundedRectangle(cornerRadius: 12, style: .continuous)
                                        .fill(FasoltTheme.paper2)
                                )
                                .overlay(
                                    RoundedRectangle(cornerRadius: 12, style: .continuous)
                                        .strokeBorder(FasoltTheme.rule1, lineWidth: FasoltTheme.hairline)
                                )
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
                        HStack(spacing: 12) {
                            Rectangle()
                                .fill(FasoltTheme.rule1)
                                .frame(height: FasoltTheme.hairline)
                            Text("or")
                                .font(.system(size: 13))
                                .foregroundStyle(FasoltTheme.ink2)
                            Rectangle()
                                .fill(FasoltTheme.rule1)
                                .frame(height: FasoltTheme.hairline)
                        }
                        .padding(.horizontal)
                    }

                    // Email — separate buttons for sign up vs sign in
                    VStack(spacing: 10) {
                        Button {
                            Task {
                                await authService.signIn(serverURL: serverURL, providerHint: "email", screenHint: "signup")
                            }
                        } label: {
                            if authService.isLoading {
                                ProgressView()
                                    .tint(FasoltTheme.accentOn)
                            } else {
                                Text("Sign up with email")
                            }
                        }
                        .buttonStyle(AccentButtonStyle(height: 48))
                        .disabled(authService.isLoading || serverURL.isEmpty)
                        .opacity(authService.isLoading || serverURL.isEmpty ? 0.5 : 1)

                        Button {
                            Task {
                                await authService.signIn(serverURL: serverURL, providerHint: "email", screenHint: "signin")
                            }
                        } label: {
                            Text("Sign in with email")
                                .font(.system(size: 17, weight: .semibold))
                                .foregroundStyle(FasoltTheme.ink0)
                                .frame(maxWidth: .infinity)
                                .frame(height: 48)
                                .background(
                                    RoundedRectangle(cornerRadius: 14, style: .continuous)
                                        .fill(FasoltTheme.paper1)
                                )
                                .overlay(
                                    RoundedRectangle(cornerRadius: 14, style: .continuous)
                                        .strokeBorder(FasoltTheme.rule1, lineWidth: FasoltTheme.hairline)
                                )
                        }
                        .buttonStyle(.plain)
                        .disabled(authService.isLoading || serverURL.isEmpty)
                        .opacity(authService.isLoading || serverURL.isEmpty ? 0.5 : 1)
                    }
                    .padding(.horizontal)

                    if let error = authService.errorMessage {
                        Text(error)
                            .font(.system(size: 13))
                            .foregroundStyle(FasoltTheme.again)
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
                        .font(.system(size: 13))
                        .foregroundStyle(FasoltTheme.ink2)
                    }

                    Spacer().frame(height: 32)
                }
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .scrollContentBackground(.hidden)
            .offlineBanner()
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
