# URL Shortener

[![CI](https://github.com/akandhari/url-shortener/actions/workflows/ci.yml/badge.svg)](https://github.com/akandhari/url-shortener/actions/workflows/ci.yml)

A URL shortener service in ASP.NET Core (.NET 10), built with AI assistance under explicit engineering control.

> **v0.1.0 (greenfield)**: create, redirect, details, web page. **v0.2.0 (brownfield)**: click analytics.
> **v0.3.0 (ambiguous)**: abuse protection (URL rules, rate limits, custom aliases, expiry, admin disable, security headers).
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
| POST | `/api/links` `{ "url": "...", "alias"?: "...", "expiresAt"?: "..." }` | **201** + link · **400** invalid URL, alias or expiry · **409** alias taken · **429** rate limit · **503** no unique code |
| GET | `/{code}` | **302** to the target (counts a click) · **410** expired or disabled · **404** · **429** |
| GET | `/api/links/{code}` | **200** link with click count and `status` (active / expired / disabled) · **404** |
| DELETE | `/api/links/{code}` (header `X-Admin-Key`) | **204** disabled · **401** wrong/missing key · **403** no key configured · **404** |
| GET | `/api/links/{code}/stats` | **200** total clicks, clicks per UTC day (last 30 days), top 5 referring sites · **404** |

Errors are [ProblemDetails](https://www.rfc-editor.org/rfc/rfc7807) JSON. In shared environments set
`ShortLinks:PublicBaseUrl` (e.g. `https://sho.rt/`), so short URLs never depend on the caller's `Host` header.

Clicks are recorded in the background (see [ADR-0003](docs/adr/0003-async-click-recording.md)), so click counts appear
a moment after the redirect.

## Configuration
All optional; defaults work for local use.

| Setting | Default | What |
|---|---|---|
| `ShortLinks:PublicBaseUrl` | request host | Base of the short URLs handed out; also blocked as a link target (no loops) |
| `RateLimits:Create` / `Redirect` / `Lookup` | 10 / 300 / 60 per minute per client IP | `PermitLimit` and `Window`; validated at startup |
| `Abuse:BlockedDomains` | empty | Domains (and subdomains) that may not be linked to |
| `Abuse:BlockedAliasWords` | empty (built-in list applies) | Extra words not allowed in custom aliases |
| `Admin:ApiKey` | not set (disable endpoint off) | **Secret: set via environment variable `Admin__ApiKey` or user secrets, never in a file** |
| `ClickRecording:QueueCapacity` / `MaxBatchSize` | 10,000 / 500 | Background click buffer |
| `RedirectCache:TimeToLive` / `MaxEntries` | 5 min / 10,000 | Redirect lookup cache |

Example: `$env:Admin__ApiKey = "<a long random value>"; dotnet run --project src/UrlShortener.Api`

## Security at a glance
Target URLs: http/https only, no credentials, no private/loopback/link-local addresses in any notation, no internal
host names, no links to ourselves. Per-client rate limits. Expiry and admin disable (410 Gone). Strict
Content-Security-Policy on the web page; security headers and HSTS everywhere. Click data stores the referrer host
only, never IP addresses. Details: [Scenario 3](docs/scenarios/03-ambiguous.md), [ADR-0004](docs/adr/0004-abuse-controls.md).

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
| [Scenario 3: Ambiguous](docs/scenarios/03-ambiguous.md) | "Make it safe from abuse": threat model, chosen scope, deferred items, validation |
| [ADRs](docs/adr/) | Key decisions with the options considered |
| [AI usage policy](docs/AI-USAGE-POLICY.md) · [AI log](docs/AI-LOG.md) · [Sign-offs](docs/SIGNOFF.md) | How AI was used and controlled |
| [Task specs](docs/prompts/) | The spec each task started from, with its iterations |
