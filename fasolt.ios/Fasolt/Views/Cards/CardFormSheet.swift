import SwiftUI

struct CardFormSheet: View {
    @Environment(\.dismiss) private var dismiss

    let mode: Mode
    let decks: [DeckDTO]
    let onSave: (CreateCardRequest, String?) async throws -> Void

    @State private var front = ""
    @State private var back = ""
    @State private var sourceFile = ""
    @State private var sourceHeading = ""
    @State private var selectedDeckId: String?
    @State private var isSaving = false
    @State private var errorMessage: String?

    enum Mode {
        case create
        case edit(front: String, back: String, sourceFile: String?, sourceHeading: String?, deckId: String?)

        var title: String {
            switch self {
            case .create: return "New Card"
            case .edit: return "Edit Card"
            }
        }
    }

    init(mode: Mode, decks: [DeckDTO], onSave: @escaping (CreateCardRequest, String?) async throws -> Void) {
        self.mode = mode
        self.decks = decks
        self.onSave = onSave

        switch mode {
        case .create:
            break
        case .edit(let front, let back, let sourceFile, let sourceHeading, let deckId):
            _front = State(initialValue: front)
            _back = State(initialValue: back)
            _sourceFile = State(initialValue: sourceFile ?? "")
            _sourceHeading = State(initialValue: sourceHeading ?? "")
            _selectedDeckId = State(initialValue: deckId)
        }
    }

    private var canSave: Bool {
        !front.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
        !back.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var body: some View {
        NavigationStack {
            Form {
                Section("Content") {
                    TextField("Front", text: $front, axis: .vertical)
                        .lineLimit(3...6)
                    TextField("Back", text: $back, axis: .vertical)
                        .lineLimit(3...6)
                }

                Section("Source (Optional)") {
                    TextField("Source File", text: $sourceFile)
                    TextField("Heading", text: $sourceHeading)
                }

                Section("Deck (Optional)") {
                    Picker("Deck", selection: $selectedDeckId) {
                        Text("None").tag(String?.none)
                        ForEach(decks, id: \.id) { deck in
                            Text(deck.name).tag(Optional(deck.id))
                        }
                    }
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

        let request = CreateCardRequest(
            front: front.trimmingCharacters(in: .whitespacesAndNewlines),
            back: back.trimmingCharacters(in: .whitespacesAndNewlines),
            sourceFile: sourceFile.isEmpty ? nil : sourceFile,
            sourceHeading: sourceHeading.isEmpty ? nil : sourceHeading
        )

        do {
            try await onSave(request, selectedDeckId)
            dismiss()
        } catch {
            errorMessage = "Failed to save card. Please try again."
        }

        isSaving = false
    }
}
