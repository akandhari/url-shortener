# URL Shortener

[![CI](https://github.com/akandhari/url-shortener/actions/workflows/ci.yml/badge.svg)](https://github.com/akandhari/url-shortener/actions/workflows/ci.yml)

A URL shortener service in ASP.NET Core (.NET 10), built with AI assistance under explicit engineering control.

> Work in progress. Requirements: [docs/REQUIREMENTS.md](docs/REQUIREMENTS.md) ·
> How I use AI: [docs/AI-USAGE-POLICY.md](docs/AI-USAGE-POLICY.md)

## Prerequisites
- .NET SDK 10.0

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
