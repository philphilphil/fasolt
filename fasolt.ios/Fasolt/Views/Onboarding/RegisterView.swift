import SwiftUI

struct RegisterView: View {
    @Environment(AuthService.self) private var authService
    @Environment(\.dismiss) private var dismiss
    @State private var viewModel = RegisterViewModel()
    @State private var showSafari = false
    @State private var showVerifyEmail = false
    let serverURL: String

    private var termsURL: URL {
        URL(string: "\(serverURL)/terms") ?? URL(string: "https://fasolt.app/terms")!
    }

    var body: some View {
        ScrollView {
            VStack(spacing: 24) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Create Account")
                        .font(.largeTitle.bold())
                    Text("Start learning with spaced repetition")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal)

                if let error = authService.errorMessage {
                    Text(error)
                        .font(.footnote)
                        .foregroundStyle(.red)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal)
                }

                VStack(spacing: 16) {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Email")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        TextField("you@example.com", text: $viewModel.email)
                            .textFieldStyle(.roundedBorder)
                            .textContentType(.emailAddress)
                            .keyboardType(.emailAddress)
                            .autocorrectionDisabled()
                            .textInputAutocapitalization(.never)
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        Text("Password")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        SecureField("Password", text: $viewModel.password)
                            .textFieldStyle(.roundedBorder)
                            .textContentType(.newPassword)
                        if !viewModel.password.isEmpty {
                            VStack(alignment: .leading, spacing: 2) {
                                ForEach(viewModel.passwordRules, id: \.label) { rule in
                                    HStack(spacing: 6) {
                                        Image(systemName: rule.valid ? "checkmark.circle.fill" : "circle")
                                            .foregroundStyle(rule.valid ? Color.green : Color.secondary)
                                            .font(.caption2)
                                        Text(rule.label)
                                            .font(.caption2)
                                            .foregroundStyle(rule.valid ? Color.primary : Color.secondary)
                                    }
                                }
                            }
                            .padding(.top, 4)
                        }
                    }

                    VStack(alignment: .leading, spacing: 4) {
                        Text("Confirm Password")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        SecureField("Confirm password", text: $viewModel.confirmPassword)
                            .textFieldStyle(.roundedBorder)
                            .textContentType(.newPassword)
                        if viewModel.passwordMismatch {
                            Text("Passwords don't match")
                                .font(.caption2)
                                .foregroundStyle(.red)
                        }
                    }

                    HStack(alignment: .top, spacing: 10) {
                        Toggle("", isOn: $viewModel.tosAccepted)
                            .toggleStyle(.switch)
                            .labelsHidden()
                        VStack(alignment: .leading, spacing: 2) {
                            Text("I agree to the")
                                .font(.footnote)
                            Button {
                                showSafari = true
                            } label: {
                                Text("Terms of Service")
                                    .font(.footnote)
                                    .underline()
                            }
                        }
                        Spacer()
                    }
                    .padding(.top, 4)
                }
                .padding(.horizontal)

                Button {
                    Task {
                        await viewModel.register(authService: authService, serverURL: serverURL)
                    }
                } label: {
                    if authService.isLoading {
                        ProgressView()
                            .frame(maxWidth: .infinity)
                            .frame(height: 22)
                    } else {
                        Text("Create Account")
                            .frame(maxWidth: .infinity)
                    }
                }
                .buttonStyle(.borderedProminent)
                .controlSize(.large)
                .disabled(!viewModel.isFormValid || authService.isLoading)
                .padding(.horizontal)
            }
            .padding(.vertical)
        }
        .navigationTitle("Create Account")
        .navigationBarTitleDisplayMode(.inline)
        .sheet(isPresented: $showSafari) {
            SafariView(url: termsURL)
                .ignoresSafeArea()
        }
        .navigationDestination(isPresented: $showVerifyEmail) {
            VerifyEmailView(email: viewModel.email) {
                // Pop the entire registration stack back to OnboardingView,
                // not just one level back to RegisterView.
                dismiss()
            }
        }
        .onChange(of: authService.registrationSuccess) { _, success in
            if success {
                authService.registrationSuccess = false
                showVerifyEmail = true
            }
        }
    }
}

#Preview {
    NavigationStack {
        RegisterView(serverURL: "https://fasolt.app")
            .environment(AuthService())
    }
}
