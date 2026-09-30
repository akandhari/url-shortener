# GF-01: Solution scaffold and quality gates

| | |
|---|---|
| **Scenario** | Greenfield |
| **Depends on** | None |
| **Requirements** | NFR: maintainability, testability, safe change management |
| **Needs my sign-off?** | Yes: CI (defines the quality gates) · dependency (test packages) |
| **Status** | Done |

## Goal
An empty but buildable solution with the final project layout and all quality gates running, both locally
and in CI. Every later task then lands on a safety net that already works.

## Context
- The repo currently holds only docs (`CLAUDE.md`, `docs/`). The branch is `master`.
- I develop on Windows (PowerShell 5.1, .NET SDK 10.0.401). CI will run on GitHub Actions (ubuntu).
- Layering is decided (see `CLAUDE.md`): Core ← Infrastructure ← Api.

## Constraints
- Target `net10.0` everywhere.
- Projects:
  - `src/UrlShortener.Core` (class library, **no package references**)
  - `src/UrlShortener.Infrastructure` (class library → Core)
  - `src/UrlShortener.Api` (ASP.NET Core empty web → Core, Infrastructure)
  - `tests/UrlShortener.UnitTests` (→ Core)
  - `tests/UrlShortener.IntegrationTests` (→ Api)
- `Directory.Build.props` at root: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`,
  `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`.
- Central package versions in `Directory.Packages.props`.
- Allowed packages, test projects only: xUnit, xunit runner, `Microsoft.NET.Test.Sdk`, `coverlet.collector`,
  `Microsoft.AspNetCore.Mvc.Testing`. **No FluentAssertions**: v8 changed to a commercial license. Plain xUnit asserts are enough.
- No EF Core, OpenAPI, Docker or domain code yet.
- `scripts/verify.ps1` is the single definition of the gates, and CI calls the same script via `pwsh`.
  It must work in both Windows PowerShell 5.1 and pwsh 7.
- Api exposes only `GET /health/live` → 200, so the integration test harness has something real to hit.

## Acceptance criteria
- [ ] `dotnet build -c Release` succeeds with 0 warnings.
- [ ] `scripts/verify.ps1` runs these steps in order and exits non-zero on the first failure:
  1. restore
  2. build
  3. `dotnet format --verify-no-changes`
  4. tests with coverage
  5. `dotnet list package --vulnerable --include-transitive` (fails if any are found)
- [ ] Unit test project has one sanity test that passes.
- [ ] Integration test uses `WebApplicationFactory<Program>` to call `/health/live` and gets 200.
- [ ] `.github/workflows/ci.yml` runs on push and PR to `master`: setup .NET 10 → `pwsh scripts/verify.ps1`, plus a gitleaks secret scan.
- [ ] CI is green on GitHub, and the README shows a CI badge plus a "Run the quality gates" section.
- [ ] Manual check: adding an unused variable makes `verify.ps1` fail (proves warnings are errors), then I revert it.

## Test plan
- Unit: `SanityTests`, only to prove the test project is wired up.
- Integration: `HealthEndpointTests.Live_returns_200`.

## Out of scope
- Domain model, EF Core/SQLite, API endpoints beyond health, OpenAPI UI, static page, Dockerfile.

## Risks
- **Local and CI gates drift apart.** Mitigation: one script used by both.
- **Strict analyzers slow down later tasks.** Mitigation: relax specific rules only with a reason written in `.editorconfig`, never by disabling warnings-as-errors.
- **gitleaks false positive.** Mitigation: an allowlist entry with a comment, reviewed by me.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | Solution (`.slnx`, the .NET 10 default), 5 projects, `Directory.Build.props`, central package versions, health endpoint + 2 tests | Build failed on CA1707 (underscores in test names). Relaxed **only for `tests/`**, reason written in `.editorconfig`, as planned under Risks. |
| 2 | Enforce unused `using`s (IDE0005) in the build | `.editorconfig` severity only, with a comment claiming that was enough | **Wrong:** the build failed, because IDE0005 needs `GenerateDocumentationFile=true`. Fixed that way, and turned off CS1591 (missing XML docs), since this is an app, not a library. The gate caught the AI's mistake. |
| 3 | Package versions | Template versions; NuGet shows newer majors (Test.Sdk 18, runner 4, coverlet 10) | AI suggested staying on the template set. **I overruled it:** upgrade to latest where nothing breaks, because staying current is cheaper now than later. Upgraded Test.Sdk 17.14.1→18.10.1, runner 3.1.4→4.0.0, coverlet 6.0.4→10.1.0, Mvc.Testing →10.0.12 (matches runtime). All gates green. |
| 3b | Also move to xUnit v3 (`xunit.v3` 4.0.1)? | Tried it: the new analyzer (xUnit1051) wanted test cancellation tokens (easy fix), but on the .NET 10 SDK xUnit v3 needs the new Microsoft Testing Platform: a `global.json` switch, a different coverage tool and new `dotnet test` arguments in `verify.ps1` and CI | **Rolled back.** That is a toolchain migration, not a version bump. Stayed on xUnit 2.9.3 (latest v2, still maintained). Possible follow-up after submission. |
| 4 | Prove the gates bite | Planted an unused variable | `verify.ps1` failed at Build with CS0219 and exit code 1. Reverted. |

## Outcome
- **Decision:** Changed (test-name rule scoped to tests, IDE0005 fix, packages upgraded)
- **AI-LOG row:** #2, #3, #4
- **Commit(s):** GF-01 commit
- **Sign-off:** SIGNOFF #1
