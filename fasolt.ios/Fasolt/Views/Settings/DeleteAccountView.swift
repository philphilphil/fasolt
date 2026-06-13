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
            ScrollView {
                VStack(spacing: 14) {
                    warningCard

                    Text("You'll be asked to confirm before anything is deleted.")
                        .font(.system(size: 14))
                        .foregroundStyle(FasoltTheme.ink2)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.horizontal, 4)

                    if let errorMessage {
                        Text(errorMessage)
                            .font(.system(size: 13))
                            .foregroundStyle(FasoltTheme.again)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .padding(16)
                            .paperCard()
                    }

                    deleteButton
                }
                .padding(.horizontal, FasoltTheme.pagePadding)
                .padding(.top, 8)
                .padding(.bottom, 24)
            }
            .background(FasoltTheme.paper0.ignoresSafeArea())
            .scrollContentBackground(.hidden)
            .navigationTitle("Delete account")
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

    // MARK: - Warning card

    private var warningCard: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 18, weight: .semibold))
                .foregroundStyle(FasoltTheme.again)

            Text("This action is permanent and cannot be undone. All your cards, decks, and study progress will be deleted.")
                .font(.system(size: 14))
                .foregroundStyle(FasoltTheme.ink0)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(16)
        .paperCard()
    }

    // MARK: - Delete button

    private var deleteButton: some View {
        Button {
            showConfirmAlert = true
        } label: {
            HStack(spacing: 8) {
                Text("Delete my account")
                if isDeleting {
                    ProgressView()
                        .tint(FasoltTheme.accentOn)
                }
            }
        }
        .buttonStyle(DestructiveButtonStyle())
        .disabled(isDeleting)
        .opacity(isDeleting ? 0.6 : 1)
        .padding(.top, 4)
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

/// Flat destructive button — status `again` (red), not the brand accent. Mirrors
/// AccentButtonStyle's shape so the destructive primary action reads consistently.
private struct DestructiveButtonStyle: ButtonStyle {
    var height: CGFloat = 50
    var radius: CGFloat = 14
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 17, weight: .semibold))
            .foregroundStyle(FasoltTheme.accentOn)
            .frame(maxWidth: .infinity)
            .frame(height: height)
            .background(
                RoundedRectangle(cornerRadius: radius, style: .continuous)
                    .fill(FasoltTheme.again)
            )
            .opacity(configuration.isPressed ? 0.85 : 1.0)
            .animation(.easeOut(duration: 0.12), value: configuration.isPressed)
    }
}
