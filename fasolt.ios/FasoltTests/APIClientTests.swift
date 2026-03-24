import Testing
import Foundation
@testable import Fasolt

@Suite("Endpoint URL Construction")
struct EndpointURLTests {

    @Test("builds URL with path only")
    func pathOnly() throws {
        let endpoint = Endpoint(path: "/api/cards", method: .get)
        let url = try endpoint.url(baseURL: "https://example.com")
        #expect(url.absoluteString == "https://example.com/api/cards")
    }

    @Test("builds URL with query items")
    func withQuery() throws {
        let endpoint = Endpoint(
            path: "/api/review/due",
            method: .get,
            queryItems: [URLQueryItem(name: "limit", value: "50")]
        )
        let url = try endpoint.url(baseURL: "https://example.com")
        #expect(url.absoluteString == "https://example.com/api/review/due?limit=50")
    }

    @Test("throws on invalid base URL")
    func invalidBase() {
        let endpoint = Endpoint(path: "/api/cards", method: .get)
        #expect(throws: APIError.self) {
            try endpoint.url(baseURL: "")
        }
    }
}

@Suite("APIError")
struct APIErrorTests {

    @Test("maps 401 to unauthorized")
    func unauthorized() {
        let error = APIError.fromStatus(401)
        #expect(error == .unauthorized)
    }

    @Test("maps 404 to notFound")
    func notFound() {
        let error = APIError.fromStatus(404)
        #expect(error == .notFound)
    }

    @Test("maps 500 to serverError")
    func serverError() {
        let error = APIError.fromStatus(500)
        #expect(error == .serverError(500))
    }
}
