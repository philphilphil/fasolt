import SwiftUI
import WebKit

struct SvgView: UIViewRepresentable {
    let svg: String

    func makeUIView(context: Context) -> WKWebView {
        let webView = WKWebView()
        webView.isOpaque = false
        webView.backgroundColor = .clear
        webView.scrollView.isScrollEnabled = false
        return webView
    }

    func updateUIView(_ webView: WKWebView, context: Context) {
        let html = """
        <html><head><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>body{margin:0;display:flex;justify-content:center;align-items:center;background:transparent}
        svg{max-width:100%;max-height:300px}</style></head>
        <body>\(svg)</body></html>
        """
        webView.loadHTMLString(html, baseURL: nil)
    }
}
