import SwiftUI

struct DeckFormSheet: View {
    @Environment(\.dismiss) private var dismiss

    let mode: Mode
    let onSave: (CreateDeckRequest) async throws -> Void

    @State private var name = ""
    @State private var description = ""
    @State private var isSaving = false
    @State private var errorMessage: String?

    enum Mode {
        case create
        case edit(DeckDTO)

        var title: String {
            switch self {
            case .create: return "New Deck"
            case .edit: return "Edit Deck"
            }
        }
    }

    init(mode: Mode, onSave: @escaping (CreateDeckRequest) async throws -> Void) {
        self.mode = mode
        self.onSave = onSave

        switch mode {
        case .create:
            break
        case .edit(let deck):
            _name = State(initialValue: deck.name)
            _description = State(initialValue: deck.description ?? "")
        }
    }

    private var canSave: Bool {
        !name.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Details") {
                    TextField("Name", text: $name)
                    TextField("Description (Optional)", text: $description, axis: .vertical)
                        .lineLimit(2...4)
                }

                if let errorMessage {
                    Section {
                        Text(errorMessage)
                            .foregroundStyle(.red)
                            .font(.caption)
                    }
                }
            }
            .navigationTitle(mode.title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button("Cancel") { dismiss() }
                }
                ToolbarItem(placement: .confirmationAction) {
                    Button("Save") {
                        Task { await save() }
                    }
                    .disabled(!canSave || isSaving)
                }
            }
        }
    }

    private func save() async {
        isSaving = true
        errorMessage = nil

        let request = CreateDeckRequest(
            name: name.trimmingCharacters(in: .whitespacesAndNewlines),
            description: description.isEmpty ? nil : description
        )

        do {
            try await onSave(request)
            dismiss()
        } catch {
            errorMessage = "Failed to save deck. Please try again."
        }

        isSaving = false
    }
}
