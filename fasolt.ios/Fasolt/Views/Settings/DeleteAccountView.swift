import SwiftUI

struct DeleteAccountView: View {
    @Environment(AuthService.self) private var authService
    @Environment(\.dismiss) private var dismiss

    let viewModel: SettingsViewModel

    @State private var password = ""
    @State private var confirmIdentity = ""
    @State private var errorMessage: String?
    @State private var isDeleting = false

    private var isExternal: Bool {
        viewModel.externalProvider != nil
    }

    private var canSubmit: Bool {
        if isDeleting { return false }
        return isExternal ? !confirmIdentity.isEmpty : !password.isEmpty
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    Label(
                        "This action is permanent and cannot be undone. All your cards, decks, and study progress will be deleted.",
                        systemImage: "exclamationmark.triangle.fill"
                    )
                    .foregroundStyle(.red)
                    .font(.subheadline)
                }

                Section {
                    if isExternal {
                        if let displayName = viewModel.displayName {
                            Text("Type your username **\(displayName)** to confirm.")
                                .font(.subheadline)
                                .foregroundStyle(.secondary)
                        } else {
                            Text("Type your username to confirm.")
                                .font(.subheadline)
                                .foregroundStyle(.secondary)
                        }
                        TextField("Username", text: $confirmIdentity)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .submitLabel(.done)
                    } else {
                        Text("Enter your password to confirm.")
                            .font(.subheadline)
                            .foregroundStyle(.secondary)
                        SecureField("Password", text: $password)
                            .textInputAutocapitalization(.never)
                            .autocorrectionDisabled()
                            .submitLabel(.done)
                    }
                }

                if let errorMessage {
                    Section {
                        Text(errorMessage)
                            .foregroundStyle(.red)
                            .font(.caption)
                    }
                }

                Section {
                    Button(role: .destructive) {
                        Task { await performDelete() }
                    } label: {
                        HStack {
                            Text("Delete my account")
                            Spacer()
                            if isDeleting {
                                ProgressView()
                            }
                        }
                    }
                    .disabled(!canSubmit)
                }
            }
            .navigationTitle("Delete Account")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") {
                        dismiss()
                    }
                    .disabled(isDeleting)
                }
            }
            .interactiveDismissDisabled(isDeleting)
        }
    }

    private func performDelete() async {
        errorMessage = nil
        isDeleting = true
        defer { isDeleting = false }

        do {
            if isExternal {
                try await viewModel.deleteAccount(confirmIdentity: confirmIdentity)
            } else {
                try await viewModel.deleteAccount(password: password)
            }
            // Server has revoked tokens and deleted data. Clear local state and
            // bounce back to the sign-in screen.
            await authService.signOut()
            dismiss()
        } catch let error as APIError {
            errorMessage = Self.message(for: error, isExternal: isExternal)
        } catch {
            errorMessage = "Failed to delete account. Please try again."
        }
    }

    private static func message(for error: APIError, isExternal: Bool) -> String {
        switch error {
        case .badRequest(let detail):
            return detail ?? (isExternal ? "Username does not match your account." : "Password is incorrect.")
        case .unauthorized:
            return "Your session has expired. Please sign in again."
        case .networkError:
            return "Network error. Please check your connection and try again."
        case .serverError(_, let detail):
            return detail ?? "The server could not delete your account. Please try again."
        default:
            return "Failed to delete account. Please try again."
        }
    }
}
