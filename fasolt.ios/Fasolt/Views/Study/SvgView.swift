import SwiftUI
import WebKit

struct SvgView: UIViewRepresentable {
    let svg: String

    func makeUIView(context: Context) -> WKWebView {
        let config = WKWebViewConfiguration()
        config.defaultWebpagePreferences.allowsContentJavaScript = false
        let webView = WKWebView(frame: .zero, configuration: config)
        webView.isOpaque = false
        webView.backgroundColor = .clear
        webView.scrollView.isScrollEnabled = false
        return webView
    }

    func updateUIView(_ webView: WKWebView, context: Context) {
        let sanitized = Self.sanitizeSVG(svg)
        let html = """
        <html><head><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>body{margin:0;display:flex;justify-content:center;align-items:center;background:transparent}
        svg{max-width:100%;max-height:300px}</style></head>
        <body>\(sanitized)</body></html>
        """
        webView.loadHTMLString(html, baseURL: nil)
    }

    /// Strip dangerous SVG elements/attributes to prevent content injection.
    /// Allows only safe SVG drawing elements and presentation attributes.
    private static func sanitizeSVG(_ raw: String) -> String {
        let dangerousTags: Set<String> = [
            "script", "foreignobject", "iframe", "object", "embed", "link",
            "style", "image", "a", "animate", "set", "handler", "listener"
        ]
        let dangerousAttrs = ["onclick", "onload", "onerror", "onmouseover",
                              "onfocus", "onblur", "xlink:href", "href"]

        var result = raw

        // Remove dangerous tags and their content
        for tag in dangerousTags {
            let patterns = [
                "<\(tag)[^>]*>[\\s\\S]*?</\(tag)>",
                "<\(tag)[^>]*/>"
            ]
            for pattern in patterns {
                if let regex = try? NSRegularExpression(pattern: pattern, options: [.caseInsensitive]) {
                    result = regex.stringByReplacingMatches(
                        in: result, range: NSRange(result.startIndex..., in: result), withTemplate: "")
                }
            }
        }

        // Remove dangerous attributes
        for attr in dangerousAttrs {
            if let regex = try? NSRegularExpression(
                pattern: "\\s\(attr)\\s*=\\s*[\"'][^\"']*[\"']",
                options: [.caseInsensitive]
            ) {
                result = regex.stringByReplacingMatches(
                    in: result, range: NSRange(result.startIndex..., in: result), withTemplate: "")
            }
        }

        // Remove data: URIs (can embed scripts)
        if let regex = try? NSRegularExpression(
            pattern: "url\\s*\\(\\s*[\"']?data:[^)]*\\)",
            options: [.caseInsensitive]
        ) {
            result = regex.stringByReplacingMatches(
                in: result, range: NSRange(result.startIndex..., in: result), withTemplate: "url()")
        }

        return result
    }
}
