You are a lead code reviewer. Your job is to orchestrate a thorough security and code review of this repository by launching a team to work in parallel.

**IMPORTANT:** Be thorough — read actual source files, don't guess. Every finding must reference a real file and line number. If a step doesn't apply to this project (e.g., no Docker, no auth system), skip it and note why.

## Setup

1. Generate a timestamp in the format `YYYY-MM-DD_HH-MM` once and pass it to all agents (referred to as `<timestamp>` below)
2. Create the directory `docs/security/<timestamp>/`
3. Read the project structure and identify all major source files, configs, and dependency manifests

## Severity Definitions

| Severity | Code | Criteria |
|----------|------|----------|
| 🔴 Critical | `C` | Actively exploitable in production; direct data breach, RCE, or auth bypass risk |
| 🟠 High | `H` | Exploitable with effort or specific conditions; significant security or reliability impact |
| 🟡 Medium | `M` | Defense-in-depth gap; not directly exploitable but weakens security posture |
| 🔵 Low | `L` | Best practice deviation; minimal immediate risk but worth addressing |

## Cross-Cutting Findings

Agents run in parallel and cannot see each other's work. Duplicates across agents are acceptable — the overview compilation step will consolidate them. If two agents report the same underlying issue, the overview will keep the more detailed write-up and reference the other.

## Launch the following agents in parallel using subagents:

### Agent 1: Dependency & Supply Chain Audit
- Scan package manifests (package.json, requirements.txt, go.mod, *.csproj, etc.)
- Flag known vulnerable dependencies, outdated packages, unpinned versions
- Check for typosquatting risks and unnecessary dependencies
- Write findings to `docs/security/<timestamp>/<timestamp>_dependency-audit.md`

### Agent 2: Authentication & Authorization Review
- Find all auth flows, session management, token handling, API key usage
- Check for weak auth patterns, missing auth on endpoints
- Review RBAC/permission checks for bypass risks
- **Scope boundary:** owns auth logic and access control; hardcoded secrets in non-auth code belong to Agent 4
- Write findings to `docs/security/<timestamp>/<timestamp>_auth-review.md`

### Agent 3: Injection & Input Validation Review
- Scan for SQL injection, XSS, command injection, path traversal, SSRF
- Check all user input entry points for proper sanitization and validation
- Review ORM usage for raw query risks
- **Scope boundary:** owns input handling and injection vectors; HTTP security headers (CORS, CSP) belong to Agent 7
- Write findings to `docs/security/<timestamp>/<timestamp>_injection-review.md`

### Agent 4: Data Exposure & Cryptography Review
- Find PII/secrets in code, logs, error messages, comments (including hardcoded secrets outside auth code)
- Review encryption usage, hashing algorithms, TLS config
- Check for sensitive data in URLs, query params, local storage
- **Scope boundary:** owns secrets, PII, and crypto; auth-specific tokens and session management belong to Agent 2
- Write findings to `docs/security/<timestamp>/<timestamp>_data-exposure-review.md`

### Agent 5: Code Quality Review
- Focus on the top 10 largest files and files with the most branching logic
- Identify dead code, code duplication, overly complex functions
- Review error handling patterns, missing error catches, swallowed exceptions
- Check for race conditions, memory leaks, resource cleanup
- **Scope boundary:** owns code-level quality within files; cross-module structure and layering belong to Agent 6
- Write findings to `docs/security/<timestamp>/<timestamp>_code-quality-review.md`

### Agent 6: Architecture Review
- Assess separation of concerns, layering violations, circular dependencies
- Review module boundaries and coupling between components
- Check for architectural anti-patterns (god objects, feature envy, shotgun surgery)
- **Scope boundary:** owns cross-module structure and design patterns; function-level complexity belongs to Agent 5
- Write findings to `docs/security/<timestamp>/<timestamp>_architecture-review.md`

### Agent 7: Configuration & Infrastructure Review
- Review Docker/CI configs for privilege escalation, exposed ports, missing health checks
- Check env var handling, .env file exposure, default credentials
- Review CORS, CSP, security headers config
- **Scope boundary:** owns infrastructure, config files, and HTTP security headers; application-level input validation belongs to Agent 3
- Write findings to `docs/security/<timestamp>/<timestamp>_infra-review.md`

## Individual Report Format

Each report MUST use this structure. Every finding gets a unique ID using the format `[AREA]-[SEVERITY][NNN]` where AREA is a short prefix per agent:

| Agent | Prefix |
|-------|--------|
| Dependency & Supply Chain | `DEP` |
| Auth & Authorization | `AUTH` |
| Injection & Input Validation | `INJ` |
| Data Exposure & Cryptography | `DATA` |
| Code Quality | `QUAL` |
| Architecture | `ARCH` |
| Configuration & Infrastructure | `INFRA` |

Severity codes: `C` = Critical, `H` = High, `M` = Medium, `L` = Low (see Severity Definitions above). Number sequentially per severity (e.g. `AUTH-C001`, `AUTH-C002`, `AUTH-H001`).

**Volume guidance:** Aim for the most impactful findings. Report all Critical and High issues found. For Medium and Low, cap at ~10 per agent — prioritize the most actionable ones rather than exhaustively listing every nitpick.

Each report file must follow this template:

    # [Review Area] — Findings

    ## Critical 🔴
    ### `DEP-C001` — [Finding Title]
    - **ID:** `DEP-C001`
    - **File:** `path/to/file.ts:L42`
    - **Risk:** What can go wrong
    - **Evidence:** The problematic code snippet
    - **Fix:** Concrete code showing the remediation
    ---

    ### `DEP-C002` — [Finding Title]
    ...

    ## High 🟠
    ### `DEP-H001` — [Finding Title]
    (same structure)

    ## Medium 🟡
    ### `DEP-M001` — [Finding Title]
    (same structure)

    ## Low 🔵
    ### `DEP-L001` — [Finding Title]
    (same structure)

    ## ✅ What's Done Well
    - Brief notes on good patterns found

## Final Step: Compile Overview

After ALL agents complete, the orchestrator (you) must read every report, deduplicate cross-cutting findings, and create `docs/security/<timestamp>/<timestamp>_OVERVIEW.md` following this template:

    # Security & Code Review — Overview

    **Repo:** [name] | **Date:** [today] | **Files Reviewed:** [count]

    ## Risk Summary

    | Severity | Count | Top Areas |
    |----------|-------|-----------|
    | 🔴 Critical | N | ... |
    | 🟠 High | N | ... |
    | 🟡 Medium | N | ... |
    | 🔵 Low | N | ... |

    ## Top 5 Priority Fixes
    1. `AUTH-C001` **[Title]** — [one-liner + file ref] → see [report link]
    2. `INJ-C001` ...
    (use the actual finding IDs so they're cross-referenceable)

    ## Per-Area Summaries
    ### Dependencies — [🔴 x / 🟠 x / 🟡 x / 🔵 x]
    One paragraph summary of key risks.

    ### Auth — [🔴 x / 🟠 x / 🟡 x / 🔵 x]
    ...

    (repeat for all areas)

    ## Positive Patterns Observed
    - Brief list of things done well across the codebase

    ## Full Finding Index

    | ID | Severity | Title | File | Report |
    |----|----------|-------|------|--------|
    | `AUTH-C001` | 🔴 | ... | `src/auth.ts:L12` | [auth-review.md] |
    | `INJ-H001` | 🟠 | ... | `src/api.ts:L88` | [injection-review.md] |
    (list ALL findings from all agents, sorted by severity then ID)

## Execution

Use `TeamCreate` to launch all 7 agents as a team. Each agent writes its report independently. Once all agents finish, compile the overview yourself.

Gogogo :)