# iOS User Registration & MCP Setup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add native user registration and MCP setup instructions to the iOS app so users can fully onboard without the web app.

**Architecture:** Native SwiftUI registration form calls `/api/identity/register` directly, then auto-triggers the existing OAuth/PKCE sign-in flow. MCP setup is a new collapsible section in Settings using `DisclosureGroup`. No backend changes needed.

**Tech Stack:** SwiftUI, ASWebAuthenticationSession (existing OAuth flow), UIKit pasteboard for copy

---

## File Map

| File | Action | Responsibility |
|------|--------|----------------|
| `Views/Onboarding/RegisterView.swift` | Create | Registration form UI |
| `ViewModels/RegisterViewModel.swift` | Create | Client-side validation, registration API call orchestration |
| `Views/Settings/McpSetupSection.swift` | Create | MCP URL display + collapsible client instructions |
| `Views/Onboarding/OnboardingView.swift` | Modify | Wrap in NavigationStack, two buttons, navigation to RegisterView |
| `Services/AuthService.swift` | Modify | Add `register()` method |
| `Views/Settings/SettingsView.swift` | Modify | Insert McpSetupSection between Account and Notifications |
| `Models/APIModels.swift` | Modify | Add `RegisterRequest` DTO |

All paths relative to `/Users/phil/Projects/fasolt/fasolt.ios/Fasolt/`.

---

### Task 1: Add RegisterRequest DTO and AuthService.register()

**Files:**
- Modify: `Models/APIModels.swift`
- Modify: `Services/AuthService.swift`

- [ ] **Step 1: Add RegisterRequest to APIModels.swift**

Add this struct in the `// MARK: - Auth` section of `APIModels.swift`, after `TokenResponse`:

```swift
struct RegisterRequest: Codable, Sendable {
    let email: String
    let password: String
}
```

- [ ] **Step 2: Add register() method to AuthService.swift**

Add this method in the `// MARK: - Public API` section of `AuthService.swift`, after the `signIn` method (after line 108):

```swift
func register(email: String, password: String, serverURL: String) async {
    isLoading = true
    errorMessage = nil

    // Temporarily save server URL for the API call
    let previousServerURL = keychain.retrieve("fasolt.serverURL")
    keychain.save(serverURL, forKey: "fasolt.serverURL")

    do {
        let body = RegisterRequest(email: email, password: password)
        let endpoint = Endpoint(path: "/api/identity/register", method: .post, body: body)
        let _: EmptyResponse = try await apiClient.unauthenticatedRequest(endpoint)

        authLogger.info("Registration succeeded, starting auto sign-in")
        // Don't set isLoading = false here — signIn will manage it
        await signIn(serverURL: serverURL)
        return
    } catch let error as APIError {
        // Restore previous server URL on failure
        if let previousServerURL {
            keychain.save(previousServerURL, forKey: "fasolt.serverURL")
        } else {
            keychain.delete("fasolt.serverURL")
        }
        errorMessage = Self.registrationErrorMessage(from: error)
    } catch {
        if let previousServerURL {
            keychain.save(previousServerURL, forKey: "fasolt.serverURL")
        } else {
            keychain.delete("fasolt.serverURL")
        }
        errorMessage = "Registration failed. Please try again."
    }

    isLoading = false
}

private static func registrationErrorMessage(from error: APIError) -> String {
    switch error {
    case .badRequest(let detail):
        guard let detail else { return "Registration failed. Please try again." }
        let lower = detail.lowercased()
        if lower.contains("duplicate") || lower.contains("already taken") {
            return "An account with this email already exists."
        }
        if lower.contains("too short") || lower.contains("too few") {
            return "Password must be at least 8 characters."
        }
        if lower.contains("uppercase") {
            return "Password must contain an uppercase letter."
        }
        if lower.contains("lowercase") {
            return "Password must contain a lowercase letter."
        }
        if lower.contains("digit") || lower.contains("number") {
            return "Password must contain a number."
        }
        if lower.contains("email") {
            return "Please enter a valid email address."
        }
        return detail
    case .serverError(_, let detail):
        return detail ?? "Registration failed. Please try again."
    case .networkError:
        return "Could not connect. Check your internet connection."
    default:
        return "Registration failed. Please try again."
    }
}
```

- [ ] **Step 3: Add EmptyResponse struct if it doesn't exist**

Check if `EmptyResponse` already exists in `APIModels.swift`. If not, add it at the bottom:

```swift
struct EmptyResponse: Decodable {}
```

Note: ASP.NET Identity's `/register` endpoint returns 200 with an empty JSON body on success. If the response is actually a JSON object, the `EmptyResponse` decode will still succeed. If the backend returns 200 with no body at all, you may need to add a `registerRaw()` variant that doesn't decode — but try `EmptyResponse` first.

- [ ] **Step 4: Verify the project builds**

Run:
```bash
cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 5: Commit**

```bash
git add fasolt.ios/Fasolt/Models/APIModels.swift fasolt.ios/Fasolt/Services/AuthService.swift
git commit -m "feat(ios): add registration API method to AuthService (#26)"
```

---

### Task 2: Create RegisterViewModel with Client-Side Validation

**Files:**
- Create: `ViewModels/RegisterViewModel.swift`

- [ ] **Step 1: Create RegisterViewModel.swift**

Create `/Users/phil/Projects/fasolt/fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift`:

```swift
import Foundation

@MainActor
@Observable
final class RegisterViewModel {
    var email = ""
    var password = ""
    var confirmPassword = ""

    var isFormValid: Bool {
        !email.isEmpty
        && email.contains("@")
        && password.count >= 8
        && password.rangeOfCharacter(from: .uppercaseLetters) != nil
        && password.rangeOfCharacter(from: .lowercaseLetters) != nil
        && password.rangeOfCharacter(from: .decimalDigits) != nil
        && password == confirmPassword
    }

    var passwordMismatch: Bool {
        !confirmPassword.isEmpty && password != confirmPassword
    }

    func register(authService: AuthService, serverURL: String) async {
        await authService.register(email: email, password: password, serverURL: serverURL)
    }
}
```

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/ViewModels/RegisterViewModel.swift
git commit -m "feat(ios): add RegisterViewModel with client-side validation (#26)"
```

---

### Task 3: Create RegisterView

**Files:**
- Create: `Views/Onboarding/RegisterView.swift`

- [ ] **Step 1: Create RegisterView.swift**

Create `/Users/phil/Projects/fasolt/fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift`:

```swift
import SwiftUI

struct RegisterView: View {
    @Environment(AuthService.self) private var authService
    @State private var viewModel = RegisterViewModel()
    let serverURL: String

    var body: some View {
        VStack(spacing: 24) {
            Spacer()

            VStack(alignment: .leading, spacing: 4) {
                Text("Create Account")
                    .font(.largeTitle.bold())
                Text("Start learning with spaced repetition")
                    .font(.subheadline)
                    .foregroundStyle(.secondary)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.horizontal)

            VStack(spacing: 16) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("Email")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    TextField("you@example.com", text: $viewModel.email)
                        .textFieldStyle(.roundedBorder)
                        .textContentType(.emailAddress)
                        .keyboardType(.emailAddress)
                        .autocorrectionDisabled()
                        .textInputAutocapitalization(.never)
                }

                VStack(alignment: .leading, spacing: 4) {
                    Text("Password")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    SecureField("Password", text: $viewModel.password)
                        .textFieldStyle(.roundedBorder)
                        .textContentType(.newPassword)
                }

                VStack(alignment: .leading, spacing: 4) {
                    Text("Confirm Password")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    SecureField("Confirm password", text: $viewModel.confirmPassword)
                        .textFieldStyle(.roundedBorder)
                        .textContentType(.newPassword)
                }

                if viewModel.passwordMismatch {
                    Text("Passwords don't match")
                        .font(.caption)
                        .foregroundStyle(.red)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }

                Text("Min 8 characters, with uppercase, lowercase, and a number")
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .padding(.horizontal)

            Button {
                Task {
                    await viewModel.register(authService: authService, serverURL: serverURL)
                }
            } label: {
                if authService.isLoading {
                    ProgressView()
                        .frame(maxWidth: .infinity)
                        .frame(height: 22)
                } else {
                    Text("Create Account")
                        .frame(maxWidth: .infinity)
                }
            }
            .buttonStyle(.borderedProminent)
            .controlSize(.large)
            .disabled(!viewModel.isFormValid || authService.isLoading)
            .padding(.horizontal)

            if let error = authService.errorMessage {
                Text(error)
                    .font(.caption)
                    .foregroundStyle(.red)
                    .multilineTextAlignment(.center)
                    .padding(.horizontal)
            }

            Spacer()
            Spacer()
        }
    }
}

#Preview {
    NavigationStack {
        RegisterView(serverURL: "https://fasolt.app")
            .environment(AuthService())
    }
}
```

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Onboarding/RegisterView.swift
git commit -m "feat(ios): add RegisterView with form fields and validation (#26)"
```

---

### Task 4: Update OnboardingView with NavigationStack and Two Buttons

**Files:**
- Modify: `Views/Onboarding/OnboardingView.swift`

- [ ] **Step 1: Replace OnboardingView.swift contents**

Replace the entire contents of `/Users/phil/Projects/fasolt/fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift` with:

```swift
import SwiftUI

struct OnboardingView: View {
    @Environment(AuthService.self) private var authService
    @State private var showServerField = false
    @State private var serverURL = AuthService.defaultServerURL
    private static let selfHostDefault = "http://localhost:8080"

    var body: some View {
        NavigationStack {
            VStack(spacing: 32) {
                Spacer()

                VStack(spacing: 8) {
                    Image(systemName: "rectangle.on.rectangle.angled")
                        .font(.system(size: 64))
                        .foregroundStyle(.tint)
                    Text("Fasolt")
                        .font(.largeTitle.bold())
                    Text("Spaced repetition for your notes")
                        .font(.subheadline)
                        .foregroundStyle(.secondary)
                }

                Spacer()

                if showServerField {
                    VStack(alignment: .leading, spacing: 4) {
                        Text("Server URL")
                            .font(.caption)
                            .foregroundStyle(.secondary)
                        TextField("https://fasolt.app", text: $serverURL)
                            .textFieldStyle(.roundedBorder)
                            .textContentType(.URL)
                            .autocorrectionDisabled()
                            .textInputAutocapitalization(.never)
                            .keyboardType(.URL)
                    }
                    .padding(.horizontal)
                    .transition(.move(edge: .bottom).combined(with: .opacity))
                }

                VStack(spacing: 12) {
                    NavigationLink {
                        RegisterView(serverURL: serverURL)
                    } label: {
                        Text("Create Account")
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.borderedProminent)
                    .controlSize(.large)
                    .disabled(serverURL.isEmpty)

                    Button {
                        Task {
                            await authService.signIn(serverURL: serverURL)
                        }
                    } label: {
                        if authService.isLoading {
                            ProgressView()
                                .frame(maxWidth: .infinity)
                                .frame(height: 22)
                        } else {
                            Text("Sign In")
                                .frame(maxWidth: .infinity)
                        }
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.large)
                    .disabled(authService.isLoading || serverURL.isEmpty)
                }
                .padding(.horizontal)

                if let error = authService.errorMessage {
                    Text(error)
                        .font(.caption)
                        .foregroundStyle(.red)
                        .multilineTextAlignment(.center)
                        .padding(.horizontal)
                }

                if !showServerField {
                    Button("Self-hosting? Change server") {
                        withAnimation {
                            serverURL = Self.selfHostDefault
                            showServerField = true
                        }
                    }
                    .font(.caption)
                    .foregroundStyle(.secondary)
                }

                Spacer()
                    .frame(height: 40)
            }
        }
    }
}

#Preview {
    OnboardingView()
        .environment(AuthService())
}
```

Key changes from original:
- Wrapped body in `NavigationStack`
- "Create Account" is now a `NavigationLink` with `.borderedProminent` (primary)
- "Sign In" changed to `.bordered` (secondary)
- Both buttons grouped in a `VStack(spacing: 12)` with shared horizontal padding

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Onboarding/OnboardingView.swift
git commit -m "feat(ios): update onboarding with NavigationStack and two buttons (#26)"
```

---

### Task 5: Create McpSetupSection for Settings

**Files:**
- Create: `Views/Settings/McpSetupSection.swift`

- [ ] **Step 1: Create McpSetupSection.swift**

Create `/Users/phil/Projects/fasolt/fasolt.ios/Fasolt/Views/Settings/McpSetupSection.swift`:

```swift
import SwiftUI

struct McpSetupSection: View {
    let serverURL: String
    @State private var copiedItem: String?

    private var mcpURL: String {
        "\(serverURL)/mcp"
    }

    var body: some View {
        Section {
            Text("Connect your AI agent to create flashcards from your notes. Copy your MCP URL and add it to your client.")
                .font(.subheadline)
                .foregroundStyle(.secondary)

            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("Your MCP URL")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    Text(mcpURL)
                        .font(.subheadline.monospaced())
                }
                Spacer()
                copyButton(text: mcpURL, id: "url")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Run in your terminal:")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    HStack {
                        Text("claude mcp add fasolt --transport http \(mcpURL)")
                            .font(.caption.monospaced())
                            .textSelection(.enabled)
                        Spacer()
                        copyButton(
                            text: "claude mcp add fasolt --transport http \(mcpURL)",
                            id: "claude-code"
                        )
                    }
                }
                .padding(.vertical, 4)
            } label: {
                Label("Claude Code", systemImage: "terminal")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("1. Go to claude.ai/settings")
                    Text("2. Click Connectors → Add")
                    Text("3. Paste your MCP URL")
                    Text("4. Authorize with your Fasolt account")
                }
                .font(.subheadline)
                .padding(.vertical, 4)
            } label: {
                Label("Claude.ai Web", systemImage: "globe")
            }

            DisclosureGroup {
                VStack(alignment: .leading, spacing: 8) {
                    Text("Add to ~/.copilot/mcp-config.json:")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                    let configJSON = """
                    {
                      "mcpServers": {
                        "fasolt": {
                          "type": "http",
                          "url": "\(mcpURL)"
                        }
                      }
                    }
                    """
                    HStack(alignment: .top) {
                        Text(configJSON)
                            .font(.caption.monospaced())
                            .textSelection(.enabled)
                        Spacer()
                        copyButton(text: configJSON, id: "copilot")
                    }
                }
                .padding(.vertical, 4)
            } label: {
                Label("GitHub Copilot CLI", systemImage: "chevron.left.forwardslash.chevron.right")
            }

            Text("You'll be asked to log in when your AI client first connects.")
                .font(.caption)
                .foregroundStyle(.secondary)
        } header: {
            Text("MCP Setup")
        }
    }

    private func copyButton(text: String, id: String) -> some View {
        Button {
            UIPasteboard.general.string = text
            withAnimation {
                copiedItem = id
            }
            Task {
                try? await Task.sleep(for: .seconds(2))
                withAnimation {
                    if copiedItem == id {
                        copiedItem = nil
                    }
                }
            }
        } label: {
            Image(systemName: copiedItem == id ? "checkmark" : "doc.on.doc")
                .font(.caption)
                .foregroundStyle(copiedItem == id ? .green : .accentColor)
        }
        .buttonStyle(.borderless)
    }
}

#Preview {
    List {
        McpSetupSection(serverURL: "https://fasolt.app")
    }
}
```

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Settings/McpSetupSection.swift
git commit -m "feat(ios): add MCP setup instructions section (#26)"
```

---

### Task 6: Add McpSetupSection to SettingsView

**Files:**
- Modify: `Views/Settings/SettingsView.swift`

- [ ] **Step 1: Insert McpSetupSection between Account and Notifications sections**

In `/Users/phil/Projects/fasolt/fasolt.ios/Fasolt/Views/Settings/SettingsView.swift`, add the MCP section after the closing `}` of the `Section("Account")` block (after line 44) and before the `Section("Notifications")` block (line 46):

```swift
                McpSetupSection(serverURL: authService.serverURL)
```

This goes between the Account section's closing `}` and the `Section("Notifications") {` line.

- [ ] **Step 2: Verify the project builds**

Run:
```bash
cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build 2>&1 | tail -5
```
Expected: `** BUILD SUCCEEDED **`

- [ ] **Step 3: Commit**

```bash
git add fasolt.ios/Fasolt/Views/Settings/SettingsView.swift
git commit -m "feat(ios): add MCP setup section to Settings (#26)"
```

---

### Task 7: End-to-End Testing with Playwright

**Files:** None (testing only)

- [ ] **Step 1: Start the full stack**

Make sure the full stack is running:
```bash
cd /Users/phil/Projects/fasolt && ./dev.sh
```

- [ ] **Step 2: Build and run the iOS app in Simulator**

```bash
cd /Users/phil/Projects/fasolt/fasolt.ios && xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' build
xcrun simctl boot "iPhone 16" 2>/dev/null || true
xcrun simctl install "iPhone 16" "$(xcodebuild -scheme Fasolt -destination 'platform=iOS Simulator,name=iPhone 16' -showBuildSettings 2>/dev/null | grep -m1 'BUILT_PRODUCTS_DIR' | awk '{print $3}')/Fasolt.app"
xcrun simctl launch "iPhone 16" com.fasolt.app
```

- [ ] **Step 3: Manual testing checklist**

Test the following in the Simulator:

**Registration flow:**
- [ ] Landing screen shows two buttons: "Create Account" (blue) and "Sign In" (dark)
- [ ] Tapping "Create Account" navigates to registration form
- [ ] Back button returns to landing
- [ ] Form shows Email, Password, Confirm Password fields with labels
- [ ] Password hint text is visible
- [ ] "Create Account" button is disabled when fields are empty
- [ ] Entering mismatched passwords shows "Passwords don't match"
- [ ] Filling valid fields enables the button
- [ ] Submitting creates account and auto-signs in (brief Safari sheet)
- [ ] User lands on Dashboard after successful registration

**Self-hosting:**
- [ ] Tapping "Self-hosting? Change server" shows URL field
- [ ] Both Create Account and Sign In use the custom server URL

**Duplicate registration:**
- [ ] Registering with an existing email shows friendly error

**MCP Setup in Settings:**
- [ ] Navigate to Settings tab
- [ ] "MCP Setup" section appears between Account and Notifications
- [ ] MCP URL shows correct server URL
- [ ] Copy button copies URL to clipboard (paste somewhere to verify)
- [ ] Claude Code section expands with command and copy button
- [ ] Claude.ai Web section expands with numbered steps
- [ ] GitHub Copilot CLI section expands with JSON config and copy button
- [ ] Auth note is visible at bottom of section

- [ ] **Step 4: Commit any fixes**

If testing reveals issues, fix them and commit:
```bash
git add -A
git commit -m "fix(ios): address registration/MCP setup testing feedback (#26)"
```
