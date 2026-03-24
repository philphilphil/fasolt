import SwiftUI

struct StudyView: View {
    var body: some View {
        NavigationStack {
            VStack {
                Text("Study session coming in Part 2")
                    .foregroundStyle(.secondary)
            }
            .navigationTitle("Study")
        }
    }
}
