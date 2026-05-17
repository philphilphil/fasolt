import SwiftUI

enum StudyMode: Sendable {
    case normal, cram
}

/// Identifiable payload for `.fullScreenCover(item:)` — bundling deckId + mode
/// into one value prevents tearing between the three separate @State variables
/// when the action is invoked.
struct StudySession: Identifiable, Sendable {
    let id = UUID()
    let deckId: String?
    let mode: StudyMode
}

struct StartStudyAction: Sendable {
    let action: @Sendable (String?, StudyMode) -> Void
    func callAsFunction(deckId: String? = nil, mode: StudyMode = .normal) {
        action(deckId, mode)
    }
}

struct StartStudyKey: EnvironmentKey {
    static let defaultValue = StartStudyAction { _, _ in }
}

extension EnvironmentValues {
    var startStudy: StartStudyAction {
        get { self[StartStudyKey.self] }
        set { self[StartStudyKey.self] = newValue }
    }
}
