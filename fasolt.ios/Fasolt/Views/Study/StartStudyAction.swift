import SwiftUI

struct StartStudyAction: Sendable {
    let action: @Sendable (String?) -> Void
    func callAsFunction(deckId: String? = nil) { action(deckId) }
}

struct StartStudyKey: EnvironmentKey {
    static let defaultValue = StartStudyAction { _ in }
}

extension EnvironmentValues {
    var startStudy: StartStudyAction {
        get { self[StartStudyKey.self] }
        set { self[StartStudyKey.self] = newValue }
    }
}
