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
        guard var components = URLComponents(string: baseURL),
              components.scheme != nil,
              components.host != nil else {
            throw APIError.invalidURL
        }
        components.path = path
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
    case unauthorized
    case notFound
    case serverError(Int)
    case networkError(String)
    case decodingError(String)

    static func fromStatus(_ code: Int) -> APIError {
        switch code {
        case 401: return .unauthorized
        case 404: return .notFound
        default: return .serverError(code)
        }
    }
}
