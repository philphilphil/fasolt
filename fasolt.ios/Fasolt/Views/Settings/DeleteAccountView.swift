import SwiftUI

struct DeleteAccountView: View {
    @Environment(AuthService.self) private var authService
    @Environment(\.dismiss) private var dismiss

    let viewModel: SettingsViewModel

    @State private var errorMessage: String?
    @State private var isDeleting = false
    @State private var showConfirmAlert = false

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
                    Text("You'll be asked to confirm before anything is deleted.")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
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
                        showConfirmAlert = true
                    } label: {
                        HStack {
                            Text("Delete my account")
                            Spacer()
                            if isDeleting {
                                ProgressView()
                            }
                        }
                    }
                    .disabled(isDeleting)
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
            .alert("Delete account?", isPresented: $showConfirmAlert) {
                Button("Cancel", role: .cancel) {}
                Button("Delete", role: .destructive) {
                    Task { await performDelete() }
                }
            } message: {
                Text("This will permanently delete your account and all your cards, decks, and study progress. This cannot be undone.")
            }
        }
    }

    private func performDelete() async {
        errorMessage = nil
        isDeleting = true
        defer { isDeleting = false }

        do {
            try await viewModel.deleteAccount()
            await authService.signOut()
            dismiss()
        } catch let error as APIError {
            errorMessage = Self.message(for: error)
        } catch {
            errorMessage = "Failed to delete account. Please try again."
        }
    }

    private static func message(for error: APIError) -> String {
        switch error {
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
