# iOS Markdown Rendering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render markdown in card content views using Textual, and strip markdown in list rows with a hand-rolled utility.

**Architecture:** Add Textual SPM dependency via XcodeGen. Replace `Text(card.front)` / `Text(card.back)` with `StructuredText(markdown:)` in study/detail views. Add `stripMarkdown()` String extension for list row display.

**Tech Stack:** Swift 6, SwiftUI, Textual (gonzalezreal/textual), XcodeGen

**Spec:** `docs/superpowers/specs/2026-03-27-ios-markdown-rendering-design.md`
**Issues:** #30, #31

---

## File Structure

| Action | File | Responsibility |
|--------|------|----------------|
| Modify | `fasolt.ios/project.yml` | Add Textual SPM package + target dependency |
| Create | `fasolt.ios/Fasolt/Utilities/String+StripMarkdown.swift` | `stripMarkdown()` String extension |
| Create | `fasolt.ios/FasoltTests/StripMarkdownTests.swift` | Unit tests for `stripMarkdown()` |
| Modify | `fasolt.ios/Fasolt/Views/Study/CardView.swift` | Replace `Text(text)` with `StructuredText` |
| Modify | `fasolt.ios/Fasolt/Views/Shared/CardDetailView.swift` | Replace `Text(card.front)` / `Text(card.back)` with `StructuredText` |
| Modify | `fasolt.ios/Fasolt/Views/Shared/CardDetailSheet.swift` | Replace `Text(card.front)` / `Text(card.back)` with `StructuredText` |
| Modify | `fasolt.ios/Fasolt/Views/Decks/DeckCardRow.swift` | Use `card.front.stripMarkdown()` |

---

### Task 1: Add Textual SPM dependency

**Files:**
- Modify: `fasolt.ios/project.yml`

- [ ] **Step 1: Add package and dependency to project.yml**

Add the `packages` section at the top level and add the package dependency to the `Fasolt` target:

```yaml
packages:
  Textual:
    url: https://github.com/gonzalezreal/textual
    from: "1.0.0"
```

And in the `Fasolt` target's `dependencies`:

```yaml
    dependencies:
      - package: Textual
```

The full modified `project.yml` should look like:

```yaml
name: Fasolt
options:
  bundleIdPrefix: com.fasolt
  deploymentTarget:
    iOS: "17.0"
  xcodeVersion: "16"
  generateEmptyDirectories: true

configFiles:
  Debug: local.xcconfig
  Release: local.xcconfig

settings:
  base:
    SWIFT_VERSION: "6.0"
    MARKETING_VERSION: "1.0.0"
    CURRENT_PROJECT_VERSION: 1

packages:
  Textual:
    url: https://github.com/gonzalezreal/textual
    from: "1.0.0"

targets:
  Fasolt:
    type: application
    platform: iOS
    sources:
      - Fasolt
    dependencies:
      - package: Textual
    info:
      path: Fasolt/Info.plist
      properties:
        CFBundleDisplayName: Fasolt
        CFBundleURLTypes:
          - CFBundleURLName: com.fasolt.oauth
            CFBundleURLSchemes:
              - fasolt
        UILaunchScreen:
          UIColorName: ""
        UIBackgroundModes:
          - remote-notification
        NSAppTransportSecurity:
          NSAllowsLocalNetworking: true
    entitlements:
      path: Fasolt/Fasolt.entitlements
      properties:
        aps-environment: production
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: com.fasolt.app
        INFOPLIST_FILE: Fasolt/Info.plist

  FasoltTests:
    type: bundle.unit-test
    platform: iOS
    sources:
      - FasoltTests
    dependencies:
      - target: Fasolt
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: com.fasolt.app.tests
        GENERATE_INFOPLIST_FILE: YES
```

- [ ] **Step 2: Regenerate Xcode project**

Run: `cd fasolt.ios && xcodegen generate`
Expected: "⚙ Generating plists..." and "Created project at ..."

- [ ] **Step 3: Resolve packages**

Run: `cd fasolt.ios && xcodebuild -resolvePackageDependencies -project Fasolt.xcodeproj -scheme Fasolt`
Expected: Package resolution succeeds, Textual is fetched.

- [ ] **Step 4: Verify it builds**

Run: `cd fasolt.ios && xcodebuild build -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```bash
cd fasolt.ios
git add project.yml Fasolt.xcodeproj
git commit -m "feat(ios): add Textual SPM dependency for markdown rendering (#30)"
```

---

### Task 2: Implement and test `stripMarkdown()` utility

**Files:**
- Create: `fasolt.ios/Fasolt/Utilities/String+StripMarkdown.swift`
- Create: `fasolt.ios/FasoltTests/StripMarkdownTests.swift`

- [ ] **Step 1: Write the failing tests**

Create `fasolt.ios/FasoltTests/StripMarkdownTests.swift`:

```swift
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd fasolt.ios && xcodebuild test -project Fasolt.xcodeproj -scheme FasoltTests -destination 'platform=iOS Simulator,name=iPhone 16' -quiet 2>&1 | tail -5`
Expected: FAIL — `stripMarkdown()` is not defined.

- [ ] **Step 3: Implement stripMarkdown()**

Create `fasolt.ios/Fasolt/Utilities/String+StripMarkdown.swift`:

```swift
import Foundation

extension String {
    func stripMarkdown() -> String {
        var result = self

        // Remove code block fences (``` or ```language)
        result = result.replacing(/^```\w*\n?/m, with: "")
        result = result.replacing(/\n?```$/m, with: "")

        // Remove image syntax ![alt](url) → alt
        result = result.replacing(/!\[([^\]]*)\]\([^)]*\)/, with: { $0.output.1 })

        // Remove link syntax [text](url) → text
        result = result.replacing(/\[([^\]]*)\]\([^)]*\)/, with: { $0.output.1 })

        // Remove heading markers
        result = result.replacing(/^#{1,6}\s+/m, with: "")

        // Remove bold markers (** or __)
        result = result.replacing(/\*\*(.+?)\*\*/, with: { $0.output.1 })
        result = result.replacing(/__(.+?)__/, with: { $0.output.1 })

        // Remove italic markers (* or _)
        result = result.replacing(/\*(.+?)\*/, with: { $0.output.1 })
        result = result.replacing(/(?<!\w)_(.+?)_(?!\w)/, with: { $0.output.1 })

        // Remove inline code backticks
        result = result.replacing(/`([^`]+)`/, with: { $0.output.1 })

        // Remove blockquote markers
        result = result.replacing(/^>\s+/m, with: "")

        // Remove unordered list markers (- or *)
        result = result.replacing(/^[-*]\s+/m, with: "")

        // Remove ordered list markers (1. 2. etc.)
        result = result.replacing(/^\d+\.\s+/m, with: "")

        // Remove strikethrough
        result = result.replacing(/~~(.+?)~~/, with: { $0.output.1 })

        // Collapse multiple spaces
        result = result.replacing(/  +/, with: " ")

        return result.trimmingCharacters(in: .whitespaces)
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd fasolt.ios && xcodebuild test -project Fasolt.xcodeproj -scheme FasoltTests -destination 'platform=iOS Simulator,name=iPhone 16' -quiet 2>&1 | tail -20`
Expected: All StripMarkdownTests pass.

- [ ] **Step 5: Commit**

```bash
cd fasolt.ios
git add Fasolt/Utilities/String+StripMarkdown.swift FasoltTests/StripMarkdownTests.swift
git commit -m "feat(ios): add stripMarkdown() utility with tests (#31)"
```

---

### Task 3: Replace Text with StructuredText in CardView (study view)

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Study/CardView.swift`

- [ ] **Step 1: Add Textual import and replace Text with StructuredText**

In `fasolt.ios/Fasolt/Views/Study/CardView.swift`, add `import Textual` at the top.

Replace the text rendering block (lines 28-34):

```swift
            ScrollView {
                Text(text)
                    .font(.title3)
                    .multilineTextAlignment(.center)
                    .foregroundStyle(.primary)
                    .padding(.horizontal, 8)
            }
            .scrollBounceBehavior(.basedOnSize)
```

With:

```swift
            ScrollView {
                StructuredText(markdown: text)
                    .font(.title3)
                    .foregroundStyle(.primary)
                    .padding(.horizontal, 8)
            }
            .scrollBounceBehavior(.basedOnSize)
```

- [ ] **Step 2: Verify it builds**

Run: `cd fasolt.ios && xcodebuild build -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
cd fasolt.ios
git add Fasolt/Views/Study/CardView.swift
git commit -m "feat(ios): render markdown in study card view (#30)"
```

---

### Task 4: Replace Text with StructuredText in CardDetailView

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Shared/CardDetailView.swift`

- [ ] **Step 1: Add Textual import and replace both Text views**

In `fasolt.ios/Fasolt/Views/Shared/CardDetailView.swift`, add `import Textual` at the top.

Replace the front text (line 25):

```swift
                    Text(card.front)
                        .font(.body)
```

With:

```swift
                    StructuredText(markdown: card.front)
                        .font(.body)
```

Replace the back text (line 47):

```swift
                    Text(card.back)
                        .font(.body)
```

With:

```swift
                    StructuredText(markdown: card.back)
                        .font(.body)
```

- [ ] **Step 2: Verify it builds**

Run: `cd fasolt.ios && xcodebuild build -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
cd fasolt.ios
git add Fasolt/Views/Shared/CardDetailView.swift
git commit -m "feat(ios): render markdown in card detail view (#30)"
```

---

### Task 5: Replace Text with StructuredText in CardDetailSheet

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Shared/CardDetailSheet.swift`

- [ ] **Step 1: Add Textual import and replace both Text views**

In `fasolt.ios/Fasolt/Views/Shared/CardDetailSheet.swift`, add `import Textual` at the top.

Replace the front text (line 22-24):

```swift
                        Text(card.front)
                            .font(.title3)
                            .multilineTextAlignment(.center)
```

With:

```swift
                        StructuredText(markdown: card.front)
                            .font(.title3)
```

Replace the back text (line 39-41):

```swift
                        Text(card.back)
                            .font(.title3)
                            .multilineTextAlignment(.center)
```

With:

```swift
                        StructuredText(markdown: card.back)
                            .font(.title3)
```

- [ ] **Step 2: Verify it builds**

Run: `cd fasolt.ios && xcodebuild build -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
cd fasolt.ios
git add Fasolt/Views/Shared/CardDetailSheet.swift
git commit -m "feat(ios): render markdown in card detail sheet (#30)"
```

---

### Task 6: Use stripMarkdown() in DeckCardRow

**Files:**
- Modify: `fasolt.ios/Fasolt/Views/Decks/DeckCardRow.swift`

- [ ] **Step 1: Replace Text(card.front) with stripped version**

In `fasolt.ios/Fasolt/Views/Decks/DeckCardRow.swift`, replace line 10:

```swift
            Text(card.front)
                .font(.body)
                .lineLimit(2)
```

With:

```swift
            Text(card.front.stripMarkdown())
                .font(.body)
                .lineLimit(2)
```

No import needed — `stripMarkdown()` is a String extension in the same module.

- [ ] **Step 2: Verify it builds**

Run: `cd fasolt.ios && xcodebuild build -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
cd fasolt.ios
git add Fasolt/Views/Decks/DeckCardRow.swift
git commit -m "feat(ios): strip markdown from card fronts in list rows (#31)"
```

---

### Task 7: Final build + test verification

- [ ] **Step 1: Run all tests**

Run: `cd fasolt.ios && xcodebuild test -project Fasolt.xcodeproj -scheme FasoltTests -destination 'platform=iOS Simulator,name=iPhone 16' -quiet 2>&1 | tail -20`
Expected: All tests pass.

- [ ] **Step 2: Run full build**

Run: `cd fasolt.ios && xcodebuild build -project Fasolt.xcodeproj -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -quiet`
Expected: BUILD SUCCEEDED
