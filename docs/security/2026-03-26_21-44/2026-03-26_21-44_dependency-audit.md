# Dependency & Supply Chain Audit — Findings

**Date:** 2026-03-26
**Scope:** fasolt.Server (.NET 10), fasolt.client (Vue 3/npm), fasolt.Tests (.NET 10), fasolt.ios (Swift/Xcode), dotnet-tools.json

---

## Critical 🔴

No findings.

---

## High 🟠

### `DEP-H001` — picomatch ReDoS vulnerability (GHSA-c2c7-rcm5-vvqj, GHSA-3v7f-55p6-f55p)
- **ID:** `DEP-H001`
- **File:** `fasolt.client/package-lock.json` (transitive via anymatch, micromatch, readdirp)
- **Risk:** ReDoS (Regular Expression Denial of Service) via crafted extglob patterns. Method injection in POSIX character classes causes incorrect glob matching. Affects the build toolchain (Vite/chokidar file watching). Exploitability in production is low since this is a dev dependency, but it affects CI builds and dev environments.
- **Evidence:** `npm audit` reports 1 high-severity vulnerability across 4 instances of picomatch <= 2.3.1 or 4.0.0-4.0.3:
  - `node_modules/anymatch/node_modules/picomatch`
  - `node_modules/micromatch/node_modules/picomatch`
  - `node_modules/picomatch`
  - `node_modules/readdirp/node_modules/picomatch`
- **Fix:** Run `npm audit fix` in `fasolt.client/` to update picomatch to a patched version. This should resolve without breaking changes.

---

### `DEP-H002` — OpenIddict 7.0.0 is 4 minor versions behind (latest: 7.4.0)
- **ID:** `DEP-H002`
- **File:** `fasolt.Server/fasolt.Server.csproj:L23-24`
- **Risk:** OpenIddict minor releases include security hardening, bug fixes, and spec compliance improvements. Running 7.0.0 when 7.4.0 is available means missing OAuth/OIDC security patches. As the auth provider for a SaaS product, this is a high-priority update.
- **Evidence:**
  ```xml
  <PackageReference Include="OpenIddict.AspNetCore" Version="7.0.0" />
  <PackageReference Include="OpenIddict.EntityFrameworkCore" Version="7.0.0" />
  ```
  `dotnet list package --outdated` confirms latest is 7.4.0.
- **Fix:** Update both packages to 7.4.0:
  ```xml
  <PackageReference Include="OpenIddict.AspNetCore" Version="7.4.0" />
  <PackageReference Include="OpenIddict.EntityFrameworkCore" Version="7.4.0" />
  ```
  Review the [OpenIddict changelog](https://github.com/openiddict/openiddict-core/releases) for breaking changes between 7.0.0 and 7.4.0 before updating.

---

### `DEP-H003` — NuGet lock files not enforced (no RestoreLockedMode)
- **ID:** `DEP-H003`
- **File:** `fasolt.Server/fasolt.Server.csproj`, `fasolt.Tests/fasolt.Tests.csproj`
- **Risk:** Lock files (`packages.lock.json`) exist and are committed to git, but `RestorePackagesWithLockFile` and `RestoreLockedMode` are not set in the `.csproj` files or a `Directory.Build.props`. This means `dotnet restore` does NOT enforce the lock file — it can silently resolve different transitive dependency versions than what was committed. An attacker who compromises a NuGet package could have a new malicious version pulled in during CI without any lock file violation error.
- **Evidence:** No `RestorePackagesWithLockFile` or `RestoreLockedMode` found in any `.csproj` or `.props` file.
- **Fix:** Create a `Directory.Build.props` at the repo root:
  ```xml
  <Project>
    <PropertyGroup>
      <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    </PropertyGroup>
  </Project>
  ```
  And in CI, pass `--locked-mode` to `dotnet restore`:
  ```bash
  dotnet restore --locked-mode
  ```

---

## Medium 🟡

### `DEP-M001` — brace-expansion / minimatch DoS vulnerability chain (GHSA-f886-m6hf-6m8v)
- **ID:** `DEP-M001`
- **File:** `fasolt.client/package-lock.json` (transitive via `@vue/test-utils` -> `js-beautify` -> `glob` -> `minimatch` -> `brace-expansion`)
- **Risk:** Zero-step brace sequence causes process hang and memory exhaustion. This is a dev-only dependency chain (test utils), but could be triggered by crafted input in test environments.
- **Evidence:** `npm audit` reports 6 moderate-severity vulnerabilities in this chain. The root cause is `@vue/test-utils` depending on `js-beautify` which pulls in old versions of `glob` (10.5.0, marked deprecated) and `minimatch`.
- **Fix:** Run `npm audit fix --force` (note: this downgrades `@vue/test-utils` to 2.2.7) or wait for `@vue/test-utils` to drop the `js-beautify` dependency. Monitor the upstream issue.

---

### `DEP-M002` — All npm dependencies use caret (^) version ranges
- **ID:** `DEP-M002`
- **File:** `fasolt.client/package.json:L14-42`
- **Risk:** Caret ranges (e.g., `^3.5.30`) allow automatic minor and patch updates. While `package-lock.json` pins exact versions for reproducible installs, any `npm install` of a new package or `npm update` can pull in new minor versions of all dependencies. This increases the surface for supply chain attacks if a maintainer publishes a compromised minor release.
- **Evidence:**
  ```json
  "vue": "^3.5.30",
  "vite": "^8.0.1",
  "dompurify": "^3.3.3",
  ```
  All 25 dependencies use `^` or `~` ranges. None are pinned to exact versions.
- **Fix:** For production dependencies that are security-sensitive (e.g., `dompurify`, `vue`, `pinia`), consider pinning to exact versions:
  ```json
  "dompurify": "3.3.3",
  "vue": "3.5.30"
  ```
  Use tools like Renovate or Dependabot to manage updates explicitly.

---

### `DEP-M003` — Missing @types/dompurify type definitions
- **ID:** `DEP-M003`
- **File:** `fasolt.client/package.json`
- **Risk:** `dompurify` is used for HTML sanitization (security-critical). Without `@types/dompurify`, TypeScript cannot type-check calls to DOMPurify's API. Misuse of the sanitization API (wrong method, wrong options) would not be caught at compile time. As of DOMPurify 3.x, types may be bundled — verify before adding.
- **Evidence:** `dompurify` is listed as a dependency but `@types/dompurify` is not in devDependencies. No other type stub file was found.
- **Fix:** Check if DOMPurify 3.3.3 bundles its own types. If not:
  ```bash
  npm install --save-dev @types/dompurify
  ```

---

### `DEP-M004` — ModelContextProtocol.AspNetCore 1.0.0 is behind latest (1.1.0)
- **ID:** `DEP-M004`
- **File:** `fasolt.Server/fasolt.Server.csproj:L25`
- **Risk:** The MCP SDK is a core integration point for this application. Version 1.1.0 may contain protocol compliance fixes, security improvements, or bug fixes relevant to the remote MCP endpoint exposed at `/mcp`.
- **Evidence:**
  ```xml
  <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.0.0" />
  ```
  `dotnet list package --outdated` confirms latest is 1.1.0.
- **Fix:** Update to 1.1.0:
  ```xml
  <PackageReference Include="ModelContextProtocol.AspNetCore" Version="1.1.0" />
  ```

---

### `DEP-M005` — Newtonsoft.Json 13.0.3 pulled in as transitive dependency
- **ID:** `DEP-M005`
- **File:** `fasolt.Server/packages.lock.json` (transitive via `Microsoft.EntityFrameworkCore.Design` -> `Microsoft.CodeAnalysis.Workspaces.MSBuild`)
- **Risk:** Newtonsoft.Json 13.0.3 is a well-known serialization library with a history of deserialization vulnerabilities. While this is only pulled in as a transitive dependency of the EF Core Design package (used at design/migration time, not runtime), it's worth noting. The latest Newtonsoft.Json is 13.0.3 so there's no update needed — this is informational.
- **Evidence:** Lock file shows `Newtonsoft.Json` 13.0.3 as transitive via `Microsoft.CodeAnalysis.Workspaces.MSBuild`.
- **Fix:** No action needed. The EF Core Design package has `<PrivateAssets>all</PrivateAssets>` which prevents it from being published with the app. Ensure deserialization of untrusted input never uses Newtonsoft.Json with `TypeNameHandling` enabled.

---

## Low 🔵

### `DEP-L001` — FSRS.Core is a niche community package
- **ID:** `DEP-L001`
- **File:** `fasolt.Server/fasolt.Server.csproj:L12`
- **Risk:** `FSRS.Core` (1.0.7) is a .NET port of the FSRS spaced repetition algorithm. As a niche community package, it may have fewer security reviewers and a higher risk of abandonment. The package is central to the product's core functionality.
- **Evidence:**
  ```xml
  <PackageReference Include="FSRS.Core" Version="1.0.7" />
  ```
- **Fix:** Monitor the package for updates and security issues. Consider vendoring the algorithm if the package becomes unmaintained, since it implements a well-documented algorithm (FSRS-5).

---

### `DEP-L002` — Outdated test infrastructure packages
- **ID:** `DEP-L002`
- **File:** `fasolt.Tests/fasolt.Tests.csproj:L12,17`
- **Risk:** `coverlet.collector` (6.0.4, latest 8.0.1) and `Microsoft.NET.Test.Sdk` (17.14.1, latest 18.3.0) are significantly behind. While test infrastructure packages are low security risk, outdated versions may miss bug fixes that affect test reliability or code coverage accuracy.
- **Evidence:**
  ```xml
  <PackageReference Include="coverlet.collector" Version="6.0.4" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
  ```
- **Fix:** Update to latest:
  ```xml
  <PackageReference Include="coverlet.collector" Version="8.0.1" />
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.3.0" />
  ```

---

### `DEP-L003` — No automated dependency update tooling detected
- **ID:** `DEP-L003`
- **File:** Repository root (missing Dependabot/Renovate config)
- **Risk:** Without automated dependency update tooling (Dependabot, Renovate), security patches for both NuGet and npm dependencies depend on manual checks. This increases the window of exposure to newly disclosed vulnerabilities.
- **Evidence:** No `.github/dependabot.yml` or `renovate.json` found in the repository.
- **Fix:** Add a `.github/dependabot.yml`:
  ```yaml
  version: 2
  updates:
    - package-ecosystem: "nuget"
      directory: "/"
      schedule:
        interval: "weekly"
    - package-ecosystem: "npm"
      directory: "/fasolt.client"
      schedule:
        interval: "weekly"
  ```

---

### `DEP-L004` — No .npmrc restricting registry scope
- **ID:** `DEP-L004`
- **File:** `fasolt.client/` (missing `.npmrc`)
- **Risk:** Without an `.npmrc` file, all packages resolve from the default npm public registry. While the project uses only well-known public packages and the `"private": true` flag prevents accidental publishing, an `.npmrc` with `registry=https://registry.npmjs.org/` makes the source explicit and provides a place to add integrity/audit configuration.
- **Evidence:** No `.npmrc` file found in the project.
- **Fix:** Low priority. Consider adding if the project ever uses scoped private packages.

---

## What's Done Well

- **Lock files committed to git:** All three lock files (`fasolt.Server/packages.lock.json`, `fasolt.Tests/packages.lock.json`, `fasolt.client/package-lock.json`) are tracked in version control, ensuring reproducible builds.
- **All npm packages have integrity hashes:** Every package in `package-lock.json` has an `integrity` field with SHA-512 hashes, protecting against tampering.
- **No known NuGet vulnerabilities:** `dotnet list package --vulnerable` reports zero vulnerabilities for both the server and test projects.
- **Microsoft packages are current:** All `Microsoft.*` and `Npgsql.*` NuGet packages are at the latest 10.x versions, matching the .NET 10 target framework.
- **Private npm package:** `"private": true` in `package.json` prevents accidental publication to npm.
- **EF Core Design package properly scoped:** The `Microsoft.EntityFrameworkCore.Design` package has `<PrivateAssets>all</PrivateAssets>`, preventing it from leaking into the published output.
- **iOS app has no third-party dependencies:** The `project.yml` shows the iOS target depends only on its own test target — no external Swift packages, CocoaPods, or Carthage dependencies, eliminating iOS supply chain risk entirely.
- **dotnet-tools.json pinned:** The `dotnet-ef` tool is pinned to version `10.0.5` with `rollForward: false`, preventing unintended tool version changes.
- **No typosquatting risks detected:** All package names are well-known, correctly spelled, and from established publishers.
- **No dependency confusion risks:** No private/scoped package names that could be squatted on public registries.
