# Requirements

This is my restatement of the brief as an engineering problem: what I'm building, what I assumed where
the brief is open, and what I left out on purpose.

## Problem
Build a URL shortener service: turn a long URL into a short link that redirects to it, record how the link
is used, and protect the service from misuse. It is a prototype, so it must run end-to-end on a laptop,
but I design and test it as if it were going to production.

## Users
- **Creator:** submits a long URL and gets a short link back.
- **Visitor:** follows a short link.
- **Operator:** runs the service and needs it healthy, observable and hard to abuse.

## Functional requirements

| ID | Requirement | Delivered in |
|---|---|---|
| FR-1 | Create a short link from an absolute `http`/`https` URL; return the code and the full short URL | v0.1 |
| FR-2 | `GET /{code}` redirects to the target URL; an unknown code returns 404 | v0.1 |
| FR-3 | Read a link's details (target, created date, click count) | v0.1 |
| FR-4 | A web page to create a link and see its details | v0.1 |
| FR-5 | Click analytics: total clicks, clicks per day, top referring sites | v0.2 |
| FR-6 | Reject unsafe or invalid target URLs with a clear error | v0.1 basic, v0.3 hardened |
| FR-7 | Limit how fast a single client can create links | v0.3 |
| FR-8 | Optional custom alias, with rules that prevent clashes and impersonation | v0.3 |
| FR-9 | Links can expire or be disabled; they then return 410 Gone | v0.3 |

## Non-functional requirements

| ID | Area | Requirement |
|---|---|---|
| NFR-1 | Performance | The redirect is the hot path: one indexed lookup and no synchronous writes (from v0.2). I measure p95 latency locally and report it; I don't claim an SLA. |
| NFR-2 | Reliability | Codes are guaranteed unique by a database constraint. Health endpoints. Buffered analytics are flushed on graceful shutdown. |
| NFR-3 | Security | Validate all input. Never redirect to unsafe schemes or internal addresses. Rate limiting and security headers. No secrets in the repo. |
| NFR-4 | Privacy | No raw IP addresses stored. Only the referrer's host is kept, not the full referrer URL. |
| NFR-5 | Maintainability | Layered design (Core / Infrastructure / Api), zero compiler warnings, tests with every behaviour change, CI quality gates. |
| NFR-6 | Operability | Runs with `dotnet run` or `docker run`, with no external services to install. |

## Ambiguities and my assumptions

| Open question | My assumption | Why | How I'd confirm |
|---|---|---|---|
| Same URL shortened twice: same code or a new one? | A new code each time | Different creators or campaigns get separate analytics | Ask product |
| Redirect status: 301 or 302? | 302 | A 301 is cached by browsers, so repeat clicks would never reach us and analytics would undercount | Ask product whether SEO matters more than analytics |
| Code length and characters | 7 characters, Base58 (no `0`/`O`/`I`/`l`), cryptographically random | ~2.2 trillion combinations, hard to guess, short enough to type, no look-alike characters when read or typed | Revisit if volume grows |
| Who can create links and view stats? | Anyone; no accounts in the prototype | Keeps the prototype focused; the gap is documented as the top follow-up (API keys) | Ask security/product |
| Which analytics matter? | Clicks per day and top referring sites | Useful without collecting personal data | Ask the people who'd use the numbers |
| What does "reliability" cover? | A single instance that behaves correctly under concurrency and shuts down cleanly | High availability needs infrastructure that's out of scope for a prototype | Agree on targets before production |
| What happens to a deleted link? | Soft-disable: it returns 410, and its analytics are kept | Links may already be printed or shared; history stays auditable | Ask product/legal about retention |
| Expected scale | Single node, SQLite | Enough for a prototype; the repository abstraction allows Postgres later | Load estimates from product |

## Out of scope (and why)
- User accounts and authentication: the biggest gap, noted as the first follow-up.
- Custom domains, QR codes, link editing: product features that don't change the architecture.
- Geo/device analytics: they need IP or user-agent processing that raises privacy questions.
- Multi-instance deployment, distributed cache or rate limiting: documented as the scaling path, not built.

## Delivery plan
I deliver in three stages. Each one is a scenario with its own branch, pull request and tag:

| Version | Scenario | Focus |
|---|---|---|
| v0.1.0 | Greenfield | Core API, persistence, web page, quality gates (FR-1 to FR-4, basic FR-6) |
| v0.2.0 | Brownfield | Click analytics as a change request against v0.1 (FR-5) |
| v0.3.0 | Ambiguous: "make it safe from abuse" | Interpret the request, then harden (FR-6 to FR-9) |

Each scenario is documented in `docs/scenarios/`. How I use AI throughout is described in `docs/AI-USAGE-POLICY.md`.
