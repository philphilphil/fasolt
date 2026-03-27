import Foundation

extension String {
    func stripMarkdown() -> String {
        var result = self

        // Remove code block fences (``` or ```language)
        result = result.regexReplace(#"^```\w*\n?"#, with: "")
        result = result.regexReplace(#"\n?```$"#, with: "")

        // Remove image syntax ![alt](url) → alt
        result = result.regexReplace(#"!\[([^\]]*)\]\([^)]*\)"#, with: "$1")

        // Remove link syntax [text](url) → text
        result = result.regexReplace(#"\[([^\]]*)\]\([^)]*\)"#, with: "$1")

        // Remove heading markers
        result = result.regexReplace(#"^#{1,6}\s+"#, with: "", multiline: true)

        // Remove bold markers (** or __)
        result = result.regexReplace(#"\*\*(.+?)\*\*"#, with: "$1")
        result = result.regexReplace(#"__(.+?)__"#, with: "$1")

        // Remove italic markers (* or _)
        result = result.regexReplace(#"\*(.+?)\*"#, with: "$1")
        result = result.regexReplace(#"(?<!\w)_(.+?)_(?!\w)"#, with: "$1")

        // Remove inline code backticks
        result = result.regexReplace(#"`([^`]+)`"#, with: "$1")

        // Remove blockquote markers
        result = result.regexReplace(#"^>\s+"#, with: "", multiline: true)

        // Remove unordered list markers (- or *)
        result = result.regexReplace(#"^[-*]\s+"#, with: "", multiline: true)

        // Remove ordered list markers (1. 2. etc.)
        result = result.regexReplace(#"^\d+\.\s+"#, with: "", multiline: true)

        // Remove strikethrough
        result = result.regexReplace(#"~~(.+?)~~"#, with: "$1")

        // Collapse multiple spaces
        result = result.regexReplace(#" {2,}"#, with: " ")

        return result.trimmingCharacters(in: .whitespaces)
    }

    private func regexReplace(_ pattern: String, with template: String, multiline: Bool = false) -> String {
        var options: NSRegularExpression.Options = []
        if multiline {
            options.insert(.anchorsMatchLines)
        }
        guard let regex = try? NSRegularExpression(pattern: pattern, options: options) else {
            return self
        }
        let range = NSRange(startIndex..., in: self)
        return regex.stringByReplacingMatches(in: self, range: range, withTemplate: template)
    }
}
