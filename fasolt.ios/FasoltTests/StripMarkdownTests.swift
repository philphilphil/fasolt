import Testing
@testable import Fasolt

@Suite("stripMarkdown")
struct StripMarkdownTests {
    @Test("strips heading markers")
    func headings() {
        #expect("# Heading".stripMarkdown() == "Heading")
        #expect("## Sub Heading".stripMarkdown() == "Sub Heading")
        #expect("### Deep".stripMarkdown() == "Deep")
    }

    @Test("strips bold markers")
    func bold() {
        #expect("**bold text**".stripMarkdown() == "bold text")
        #expect("__bold text__".stripMarkdown() == "bold text")
    }

    @Test("strips italic markers")
    func italic() {
        #expect("*italic text*".stripMarkdown() == "italic text")
        #expect("_italic text_".stripMarkdown() == "italic text")
    }

    @Test("strips inline code backticks")
    func inlineCode() {
        #expect("`code`".stripMarkdown() == "code")
    }

    @Test("strips code block fences")
    func codeBlockFences() {
        #expect("```swift\nlet x = 1\n```".stripMarkdown() == "let x = 1")
    }

    @Test("strips link syntax preserving text")
    func links() {
        #expect("[click here](https://example.com)".stripMarkdown() == "click here")
    }

    @Test("strips image syntax preserving alt text")
    func images() {
        #expect("![alt text](image.png)".stripMarkdown() == "alt text")
    }

    @Test("strips unordered list markers")
    func unorderedLists() {
        #expect("- item one".stripMarkdown() == "item one")
        #expect("* item two".stripMarkdown() == "item two")
    }

    @Test("strips ordered list markers")
    func orderedLists() {
        #expect("1. first".stripMarkdown() == "first")
        #expect("12. twelfth".stripMarkdown() == "twelfth")
    }

    @Test("strips blockquote markers")
    func blockquotes() {
        #expect("> quoted text".stripMarkdown() == "quoted text")
    }

    @Test("handles mixed markdown")
    func mixed() {
        #expect("## **Bold heading** with `code`".stripMarkdown() == "Bold heading with code")
    }

    @Test("passes through plain text unchanged")
    func plainText() {
        #expect("just plain text".stripMarkdown() == "just plain text")
    }

    @Test("handles empty string")
    func emptyString() {
        #expect("".stripMarkdown() == "")
    }
}
