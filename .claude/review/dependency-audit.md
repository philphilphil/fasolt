# Dependency & Supply Chain Audit — Findings

## Critical 🔴

No findings.

## High 🟠

### `DEP-H001` — Wildcard NuGet Version Ranges in MCP Project
- **ID:** `DEP-H001`
- **File:** `fasolt.Mcp/fasolt.Mcp.csproj:L14-16`
- **Risk:** Wildcard version specifiers (`0.*`, `10.*`) allow NuGet to resolve any matching version at restore time, including newly published versions that could contain breaking changes, bugs, or — in a supply chain attack — malicious code. Because there is no NuGet lock file (`packages.lock.json`), builds are non-reproducible: different developers or CI runs may get different package versions.
- **Evidence:**
  ```xml
  <PackageReference Include="ModelContextProtocol" Version="0.*" />
  <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.*" />
  <PackageReference Include="Microsoft.Extensions.Http" Version="10.*" />
  ```
- **Fix:** Pin all three packages to exact versions (e.g., `0.1.0-preview.10`, `10.0.5`, `10.0.5`). Additionally, enable NuGet lock files across all projects by adding `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` to a `Directory.Build.props` and committing the generated `packages.lock.json` files.
---

### `DEP-H002` — Docker Image Tag Not Pinned to Digest
- **ID:** `DEP-H002`
- **File:** `docker-compose.yml:L3`
- **Risk:** `postgres:17` is a floating tag. The upstream image can be replaced at any time. A compromised or buggy image push would silently affect all new `docker compose pull` runs. In a supply chain attack scenario, the database container could be replaced with a trojanized version.
- **Evidence:**
  ```yaml
  image: postgres:17
  ```
- **Fix:** Pin to a specific minor version and SHA256 digest, e.g.:
  ```yaml
  image: postgres:17.5@sha256:<digest>
  ```
  Periodically update the digest using `docker pull` and `docker inspect --format='{{index .RepoDigests 0}}'`.
---

### `DEP-H003` — No NuGet Lock Files Committed
- **ID:** `DEP-H003`
- **File:** Project-wide (all `.csproj` files)
- **Risk:** Without `packages.lock.json` files, NuGet package resolution is non-deterministic. Different machines or CI environments may resolve different transitive dependency versions, making builds non-reproducible and opening a window for dependency confusion or substitution attacks.
- **Evidence:** No `packages.lock.json` files found anywhere in the repository. No `Directory.Build.props` enabling `RestorePackagesWithLockFile`.
- **Fix:** Add a `Directory.Build.props` at the repo root:
  ```xml
  <Project>
    <PropertyGroup>
      <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
    </PropertyGroup>
  </Project>
  ```
  Run `dotnet restore` for each project and commit the generated `packages.lock.json` files. Add `--locked-mode` to CI restore commands.
---

## Medium 🟡

### `DEP-M001` — ModelContextProtocol on Pre-release 0.x Version
- **ID:** `DEP-M001`
- **File:** `fasolt.Mcp/fasolt.Mcp.csproj:L14`
- **Risk:** The `ModelContextProtocol` package is at version `0.*`, which is pre-release. Pre-release packages have no stability guarantees and their API surface can change at any time. Combined with the wildcard specifier (see DEP-H001), any 0.x release — including ones with breaking changes — will be pulled in automatically.
- **Evidence:**
  ```xml
  <PackageReference Include="ModelContextProtocol" Version="0.*" />
  ```
- **Fix:** Pin to the specific 0.x version you have tested against (e.g., `0.1.0-preview.10`). Monitor the package's release notes and upgrade deliberately.
---

### `DEP-M002` — npm Caret Ranges Allow Minor/Patch Drift
- **ID:** `DEP-M002`
- **File:** `fasolt.client/package.json:L14-41`
- **Risk:** All npm dependencies use caret (`^`) ranges, which allow automatic minor and patch version updates. While `package-lock.json` is present (which mitigates this for consistent installs), any `npm install` adding a new package will re-resolve all ranges, potentially pulling in new versions of existing dependencies. Combined with the large number of transitive dependencies in the Vue/Vite ecosystem, this increases the supply chain attack surface.
- **Evidence:**
  ```json
  "vue": "^3.5.30",
  "vite": "^8.0.1",
  "pinia": "^3.0.4",
  ```
  (all 23 dependencies use `^` ranges)
- **Fix:** This is standard practice for npm projects and the lock file mitigates the immediate risk. For higher assurance, consider running `npm audit` in CI, and periodically reviewing `npm outdated`. For production deployments, use `npm ci` (which respects the lock file strictly) rather than `npm install`.
---

### `DEP-M003` — No .npmrc Restricting Registry
- **ID:** `DEP-M003`
- **File:** `fasolt.client/` (missing `.npmrc`)
- **Risk:** Without an `.npmrc` file locking the registry to `https://registry.npmjs.org`, npm could be configured via user or system-level `.npmrc` to use a different registry, enabling dependency confusion attacks in corporate/CI environments.
- **Evidence:** No `.npmrc` file found in the project.
- **Fix:** Create `fasolt.client/.npmrc`:
  ```ini
  registry=https://registry.npmjs.org
  ```
---

### `DEP-M004` — EF Core Design Package Included in Production Build
- **ID:** `DEP-M004`
- **File:** `fasolt.Server/fasolt.Server.csproj:L13-16`
- **Risk:** `Microsoft.EntityFrameworkCore.Design` is a design-time tool (for `dotnet ef` commands) and should not be deployed to production. While `PrivateAssets=all` prevents it from flowing to dependent projects, it still gets restored and its assemblies are available in the build output, increasing attack surface.
- **Evidence:**
  ```xml
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.5">
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    <PrivateAssets>all</PrivateAssets>
  </PackageReference>
  ```
- **Fix:** The `PrivateAssets=all` is correctly set, which is the standard recommended pattern. For additional hardening, consider conditionally including this package only in development configurations, or keeping migration commands in a separate project.
---

## Low 🔵

### `DEP-L001` — TypeScript Pinned with Tilde Range
- **ID:** `DEP-L001`
- **File:** `fasolt.client/package.json:L37`
- **Risk:** TypeScript uses a tilde (`~`) range instead of caret, which is intentionally more conservative (patch-only updates). This is actually a good practice for TypeScript since minor versions can introduce new type-checking behavior. Noted for completeness — no action needed.
- **Evidence:**
  ```json
  "typescript": "~5.9.3"
  ```
- **Fix:** No action needed. This is the correct approach for TypeScript.
---

### `DEP-L002` — global.json Uses rollForward: latestMinor
- **ID:** `DEP-L002`
- **File:** `global.json:L4`
- **Risk:** `rollForward: latestMinor` allows the SDK to roll forward to a newer minor version if `10.0.100` is not found. This could lead to inconsistent build behavior across developer machines with different SDK versions installed.
- **Evidence:**
  ```json
  {
    "sdk": {
      "version": "10.0.100",
      "rollForward": "latestMinor"
    }
  }
  ```
- **Fix:** For stricter reproducibility, consider using `"rollForward": "latestPatch"` which only allows patch-level differences. However, `latestMinor` is reasonable for a project actively tracking the latest .NET release.
---

### `DEP-L003` — package-lock.json Has Stale Package Name
- **ID:** `DEP-L003`
- **File:** `fasolt.client/package-lock.json:L2`
- **Risk:** The lock file references `"name": "spaced-md.client"` while `package.json` uses `"name": "fasolt.client"`. This mismatch suggests the lock file was carried over from a rename and may not have been fully regenerated. While npm handles this gracefully, it could cause confusion.
- **Evidence:**
  ```json
  "name": "spaced-md.client"  // in package-lock.json
  "name": "fasolt.client"     // in package.json
  ```
- **Fix:** Run `npm install` (or `rm package-lock.json && npm install`) to regenerate the lock file with the correct name.
---

## ✅ What's Done Well
- **Exact version pinning on .NET server packages**: `fasolt.Server.csproj` pins all four NuGet packages to exact versions (e.g., `10.0.5`), preventing version drift.
- **Exact version pinning on test packages**: `fasolt.Tests.csproj` also pins all six packages to exact versions.
- **EF Core Design marked as PrivateAssets**: The `Microsoft.EntityFrameworkCore.Design` package correctly uses `PrivateAssets=all` to prevent leaking into published output.
- **package-lock.json committed**: The npm lock file is present, which ensures deterministic installs when using `npm ci`.
- **Private package flag set**: `"private": true` in `package.json` prevents accidental publishing to npm.
- **Minimal dependency footprint**: Both the backend (4 packages) and MCP server (3 packages) have lean dependency trees, reducing overall supply chain risk.
- **No unnecessary runtime dependencies**: The frontend dependencies are well-curated for a Vue 3 + shadcn-vue stack with no bloat or suspicious packages.
- **Docker volume for data persistence**: Postgres data is stored in a named volume, which is the correct pattern.
