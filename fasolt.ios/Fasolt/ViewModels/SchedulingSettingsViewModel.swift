import Foundation

@MainActor
@Observable
final class SchedulingSettingsViewModel {
    var desiredRetention: Double = 0.9
    var maximumInterval: Int = 36500
    var dayStartHour: Int = 4
    var timeZone: String = "UTC"
    var isLoading = false
    var errorMessage: String?
    var successMessage: String?

    private let apiClient: APIClient

    init(apiClient: APIClient) {
        self.apiClient = apiClient
    }

    /// IANA timezone identifiers known to the device, sorted for display.
    var availableTimeZones: [String] {
        TimeZone.knownTimeZoneIdentifiers.sorted()
    }

    var deviceTimeZone: String {
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
            timeZone = response.timeZone
        } catch {
            errorMessage = "Could not load scheduling settings."
        }

        isLoading = false
    }

    func save() async {
        isLoading = true
        errorMessage = nil
        successMessage = nil

        let endpoint = Endpoint(
            path: "/api/settings/scheduling",
            method: .put,
            body: UpdateSchedulingSettingsRequest(
                desiredRetention: desiredRetention,
                maximumInterval: maximumInterval,
                dayStartHour: dayStartHour,
                timeZone: timeZone
            )
        )

        do {
            let response: SchedulingSettingsResponse = try await apiClient.request(endpoint)
            desiredRetention = response.desiredRetention
            maximumInterval = response.maximumInterval
            dayStartHour = response.dayStartHour
            timeZone = response.timeZone
            successMessage = "Settings saved."
        } catch {
            errorMessage = "Could not save scheduling settings."
        }

        isLoading = false
    }
}
