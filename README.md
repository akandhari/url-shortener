# URL Shortener

[![CI](https://github.com/akandhari/url-shortener/actions/workflows/ci.yml/badge.svg)](https://github.com/akandhari/url-shortener/actions/workflows/ci.yml)

A URL shortener service in ASP.NET Core (.NET 10), built with AI assistance under explicit engineering control.

> Work in progress. **v0.1.0 (greenfield)**: create, redirect, details, web page. **v0.2.0 (brownfield)**: click analytics.
> Requirements: [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) ·
> How I use AI: [docs/AI-USAGE-POLICY.md](docs/AI-USAGE-POLICY.md)

## Prerequisites
- .NET SDK 10.0. Nothing else: the database is a local SQLite file, created on first start.

## Run it
```powershell
dotnet run --project src/UrlShortener.Api
```
Then open:

| URL | What |
|---|---|
| http://localhost:5173/ | Web page: shorten a URL, copy it, see its clicks |
| http://localhost:5173/scalar | Interactive API documentation |
| http://localhost:5173/health/ready | Readiness (database reachable) |

Sample requests for every endpoint and error case: [http/UrlShortener.http](http/UrlShortener.http).

## API
| Method | Route | Result |
|---|---|---|
| POST | `/api/links` `{ "url": "..." }` | **201** + link · **400** invalid URL · **503** no unique code |
| GET | `/{code}` | **302** to the target (counts a click) · **404** |
| GET | `/api/links/{code}` | **200** link with click count · **404** |
| GET | `/api/links/{code}/stats` | **200** total clicks, clicks per UTC day (last 30 days), top 5 referring sites · **404** |

Errors are [ProblemDetails](https://www.rfc-editor.org/rfc/rfc7807) JSON. In shared environments set
`ShortLinks:PublicBaseUrl` (e.g. `https://sho.rt/`), so short URLs never depend on the caller's `Host` header.

Clicks are recorded in the background (see [ADR-0003](docs/adr/0003-async-click-recording.md)), so click counts appear
a moment after the redirect. Tunable in configuration: `ClickRecording:QueueCapacity`, `ClickRecording:MaxBatchSize`,
`RedirectCache:TimeToLive`, `RedirectCache:MaxEntries`.

## Run the quality gates
One script runs every gate in order and stops at the first failure. CI runs the same script.

```powershell
./scripts/verify.ps1
```

| Gate | What it checks |
|---|---|
| Restore | Packages resolve (versions are central in `Directory.Packages.props`) |
| Build | Compiles with **warnings as errors** and .NET analyzers on |
| Format | `dotnet format --verify-no-changes` against `.editorconfig` |
| Tests | Unit + integration tests, with code coverage |
| Vulnerable packages | No known-vulnerable direct or transitive packages |

CI also runs a **gitleaks** secret scan over the full git history.

## Project layout
```
src/UrlShortener.Core            domain and use-cases (no framework dependencies)
src/UrlShortener.Infrastructure  persistence and background services
src/UrlShortener.Api             HTTP endpoints
tests/UrlShortener.UnitTests
tests/UrlShortener.IntegrationTests
```

## Documentation
| Doc | What |
|---|---|
| [Requirements](docs/REQUIREMENTS.md) | Problem, FR/NFR, ambiguities and assumptions, delivery plan |
| [Scenario 1: Greenfield](docs/scenarios/01-greenfield.md) | Decomposition, execution, validation, known limitations |
| [Scenario 2: Brownfield](docs/scenarios/02-brownfield.md) | Change request, impact analysis, the lost-update bug (red → green), validation |
| [ADRs](docs/adr/) | Key decisions with the options considered |
| [AI usage policy](docs/AI-USAGE-POLICY.md) · [AI log](docs/AI-LOG.md) · [Sign-offs](docs/SIGNOFF.md) | How AI was used and controlled |
| [Task specs](docs/prompts/) | The spec each task started from, with its iterations |
