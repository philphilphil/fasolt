import Foundation

@MainActor
@Observable
final class SchedulingSettingsViewModel {
    var desiredRetention: Double = 0.9
    var maximumInterval: Int = 36500
    var dayStartHour: Int = 4
    var isLoading = false
    var errorMessage: String?
    var successMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    private var deviceTimeZone: String {
        TimeZone.current.identifier
    }

    func load() async {
        isLoading = true
        errorMessage = nil

        do {
            let endpoint = Endpoint(path: "/api/settings/scheduling", method: .get)
            let response: SchedulingSettingsResponse = try await apiClient.request(endpoint)
            desiredRetention = response.desiredRetention
            maximumInterval = response.maximumInterval
            dayStartHour = response.dayStartHour

            if response.timeZone != deviceTimeZone {
                try await pushSettings()
            }
        } catch {
            errorMessage = "Could not load scheduling settings."
        }

        isLoading = false
    }

    func save() async {
        isLoading = true
        errorMessage = nil
        successMessage = nil

        do {
            try await pushSettings()
            successMessage = "Settings saved."
        } catch {
            errorMessage = "Could not save scheduling settings."
        }

        isLoading = false
    }

    private func pushSettings() async throws {
        let endpoint = Endpoint(
            path: "/api/settings/scheduling",
            method: .put,
            body: UpdateSchedulingSettingsRequest(
                desiredRetention: desiredRetention,
                maximumInterval: maximumInterval,
                dayStartHour: dayStartHour,
                timeZone: deviceTimeZone
            )
        )
        let response: SchedulingSettingsResponse = try await apiClient.request(endpoint)
        desiredRetention = response.desiredRetention
        maximumInterval = response.maximumInterval
        dayStartHour = response.dayStartHour
    }
}
