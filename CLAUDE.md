# Instructions for AI assistants in this repo

I use Claude Code as a pair-programming assistant on this project. These are the rules it works under.
I update this file when I find a rule was missing.

## Project
URL shortener on ASP.NET Core (.NET 10). Three projects:
- `UrlShortener.Core`: domain and use-cases. No EF Core or ASP.NET references.
- `UrlShortener.Infrastructure`: EF Core + SQLite, background services.
- `UrlShortener.Api`: Minimal API endpoints, static page.

## How to work
- One task at a time, from its spec in `docs/prompts/<ID>.md`. If the spec is unclear, ask before writing code.
- Keep changes small: one task, roughly one commit.
- Every behaviour change comes with tests. Never weaken or delete a test to make it pass.
- Run `scripts/verify.ps1` before saying a task is done, and report the result as it is.

## Ask me first before
- adding or upgrading a NuGet package
- creating or changing a database migration
- changing security settings (URL validation, rate limits, headers)
- changing the shape of a public API endpoint
- changing CI or the quality gates

## Code style
- Nullable enabled, zero warnings, async all the way, pass `CancellationToken` through.
- Errors returned as ProblemDetails.
- No secrets, keys or credentials in code or config files.
