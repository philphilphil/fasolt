import SwiftUI

struct CardFormSheet: View {
    @Environment(\.dismiss) private var dismiss

    let mode: Mode
    let decks: [DeckDTO]
    let onSave: (CreateCardRequest, [String]) async throws -> Void
    var onToggleSuspended: ((Bool) async throws -> Void)?

    @State private var front = ""
    @State private var back = ""
    @State private var sourceFile = ""
    @State private var sourceHeading = ""
    @State private var selectedDeckIds: Set<String> = []
    @State private var isSuspended = false
    @State private var isSaving = false
    @State private var errorMessage: String?

    enum Mode {
        case create(initialDeckIds: [String] = [])
        case edit(front: String, back: String, sourceFile: String?, sourceHeading: String?, deckIds: [String], isSuspended: Bool)

        var title: String {
            switch self {
            case .create: return "New Card"
            case .edit: return "Edit Card"
            }
        }

        var isCreate: Bool {
            if case .create = self { return true }
            return false
        }
    }

    init(mode: Mode, decks: [DeckDTO], onSave: @escaping (CreateCardRequest, [String]) async throws -> Void, onToggleSuspended: ((Bool) async throws -> Void)? = nil) {
        self.mode = mode
        self.decks = decks
        self.onSave = onSave
        self.onToggleSuspended = onToggleSuspended

        switch mode {
        case .create(let initialDeckIds):
            _selectedDeckIds = State(initialValue: Set(initialDeckIds))
        case .edit(let front, let back, let sourceFile, let sourceHeading, let deckIds, let isSuspended):
            _front = State(initialValue: front)
            _back = State(initialValue: back)
            _sourceFile = State(initialValue: sourceFile ?? "")
            _sourceHeading = State(initialValue: sourceHeading ?? "")
            _selectedDeckIds = State(initialValue: Set(deckIds))
            _isSuspended = State(initialValue: isSuspended)
        }
    }

    private var canSave: Bool {
        !front.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty &&
        !back.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    private var deckSectionFooter: String {
        if mode.isCreate {
            return "Card will be added to all selected decks. The first selection is used for the initial create."
        }
        return "A card can be assigned to multiple decks."
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

                if !decks.isEmpty {
                    Section {
                        ForEach(decks, id: \.id) { deck in
                            Button {
                                toggleDeck(deck.id)
                            } label: {
                                HStack {
                                    Text(deck.name)
                                        .foregroundStyle(.primary)
                                    Spacer()
                                    if selectedDeckIds.contains(deck.id) {
                                        Image(systemName: "checkmark")
                                            .foregroundStyle(.blue)
                                    }
                                }
                                .contentShape(Rectangle())
                            }
                        }
                    } header: {
                        Text("Decks")
                    } footer: {
                        Text(deckSectionFooter)
                    }
                }

                if case .edit = mode, onToggleSuspended != nil {
                    Section {
                        Toggle("Suspended", isOn: $isSuspended)
                    } footer: {
                        Text("Suspended cards are excluded from review sessions.")
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

    private func toggleDeck(_ id: String) {
        if selectedDeckIds.contains(id) {
            selectedDeckIds.remove(id)
        } else {
            selectedDeckIds.insert(id)
        }
    }

    private func save() async {
        isSaving = true
        errorMessage = nil

        let request = CreateCardRequest(
            front: front.trimmingCharacters(in: .whitespacesAndNewlines),
            back: back.trimmingCharacters(in: .whitespacesAndNewlines),
            sourceFile: sourceFile.isEmpty ? nil : sourceFile,
            sourceHeading: sourceHeading.isEmpty ? nil : sourceHeading,
            deckId: nil
        )

        do {
            // Preserve deck order matching `decks` so the first selection is stable.
            let ordered = decks.map(\.id).filter { selectedDeckIds.contains($0) }
            try await onSave(request, ordered)
            if case .edit(_, _, _, _, _, let wasSuspended) = mode, wasSuspended != isSuspended {
                try await onToggleSuspended?(isSuspended)
            }
            dismiss()
        } catch {
            errorMessage = "Failed to save card. Please try again."
        }

        isSaving = false
    }
}
