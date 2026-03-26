import Foundation

enum HTTPMethod: String, Sendable {
    case get = "GET"
    case post = "POST"
    case put = "PUT"
    case delete = "DELETE"
}

struct Endpoint: Sendable {
    let path: String
    let method: HTTPMethod
    var queryItems: [URLQueryItem]? = nil
    var body: (any Encodable & Sendable)? = nil

    func url(baseURL: String) throws -> URL {
        guard let baseURL = URL(string: baseURL) else {
            throw APIError.invalidURL
        }
        guard var components = URLComponents(url: baseURL.appendingPathComponent(path), resolvingAgainstBaseURL: true) else {
            throw APIError.invalidURL
        }
        if let queryItems, !queryItems.isEmpty {
            components.queryItems = queryItems
        }
        guard let url = components.url else {
            throw APIError.invalidURL
        }
        return url
    }
}

enum APIError: Error, Equatable {
    case invalidURL
    case badRequest(String?)
    case unauthorized
    case forbidden
    case notFound
    case serverError(Int, String?)
    case networkError(String)
    case decodingError(String)

    static func fromStatus(_ code: Int, detail: String? = nil) -> APIError {
        switch code {
        case 400: return .badRequest(detail)
        case 401: return .unauthorized
        case 403: return .forbidden
        case 404: return .notFound
        default: return .serverError(code, detail)
        }
    }
}
