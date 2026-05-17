import SwiftUI

enum StudyMode: Sendable {
    case normal, cram
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
