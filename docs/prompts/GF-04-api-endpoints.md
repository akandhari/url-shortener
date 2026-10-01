# GF-04: API endpoints, redirect with click count, OpenAPI

| | |
|---|---|
| **Scenario** | Greenfield |
| **Depends on** | GF-03 |
| **Requirements** | FR-1, FR-2, FR-3, FR-6 |
| **Needs my sign-off?** | Yes: public API contract · dependencies (`Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`) |
| **Status** | Done |

## Goal
The service is usable over HTTP: create a short link, follow it, and read its details. Errors have one
consistent shape, and the API documents itself in the browser.

## Context
- `LinkService` (Core) does create/resolve; `EfLinkRepository` stores links in SQLite (GF-03).
- `ShortLink.ClickCount` exists but nothing increments it yet.
- Decided in `docs/REQUIREMENTS.md`: 302 (not 301) so every click reaches us; a new code per request.

## API contract (needs sign-off)
| Method | Route | Success | Errors |
|---|---|---|---|
| POST | `/api/links` body `{ "url": "..." }` | **201 Created**, `Location: /api/links/{code}`, body = link | **400** invalid URL (ProblemDetails with the reason); **503** no unique code after 5 tries |
| GET | `/{code}` | **302 Found**, `Location: <target>`; click counted | **404** unknown or malformed code |
| GET | `/api/links/{code}` | **200**, body = link | **404** |

Link body:
```json
{ "code": "k3X9aQ2", "shortUrl": "https://host/k3X9aQ2", "targetUrl": "https://example.com/page",
  "createdAt": "2026-09-30T12:00:00+00:00", "clickCount": 0 }
```

## Constraints
- **Click counting is deliberately naive in v0.1:** on redirect, load the link, `ClickCount++`, save
  (read-modify-write). It's simple and fine for one user at a time. It's documented as a known simplification and
  revisited in the brownfield scenario (CR-002).
- `shortUrl` is built from a configured public base URL (`ShortLinks:PublicBaseUrl`). It falls back to the
  request's scheme and host only when that setting is absent (local development). Reason: building it from the
  `Host` header lets a caller inject any host into the links we hand out.
- All errors are RFC 7807 ProblemDetails (`AddProblemDetails`, exception handler, status code pages). No stack
  traces in responses.
- OpenAPI document at `/openapi/v1.json` (built into .NET 10 via `Microsoft.AspNetCore.OpenApi`), browsable UI at
  `/scalar` (`Scalar.AspNetCore`). Enabled in all environments for this prototype so reviewers can use it
  (trade-off noted: in production, restrict or disable it).
- Endpoints in their own file (`Endpoints/LinkEndpoints.cs`), not piled into `Program.cs`.
- Literal routes (`/api/...`, `/health/...`, `/scalar`, `/openapi/...`) must win over `/{code}`.

## Acceptance criteria
- [ ] POST valid URL → 201, `Location` header, body with a well-formed 7-character code and `clickCount` 0.
- [ ] POST invalid URL (`javascript:alert(1)`, empty, relative) → 400 ProblemDetails whose `detail` says why.
- [ ] POST with no body or malformed JSON → 400 ProblemDetails, not 500.
- [ ] GET `/{code}` → 302 with `Location` = target; the details endpoint then shows `clickCount` 1.
- [ ] GET `/{unknown}` and `/{malformed}` → 404 ProblemDetails.
- [ ] GET `/api/links/{code}` → 200 with the same body shape as create.
- [ ] `shortUrl` uses `PublicBaseUrl` when configured, even if the request's `Host` header says otherwise.
- [ ] `/openapi/v1.json` → 200 and lists the three link endpoints; `/scalar` → 200.
- [ ] `/health/live` and `/health/ready` still work (route precedence).
- [ ] `scripts/verify.ps1` passes.

## Test plan
- Integration (`LinkEndpointsTests`, via `ApiFactory`, `HttpClient` with auto-redirect **off**): every criterion above.
- Unit: `ShortLink.RegisterClick` increments the count.

## Out of scope
- Static page (GF-05), stats/analytics (BF-07), rate limiting, alias, expiry (AB), hardened URL rules (AB-02).

## Risks
- **Naive click counting loses clicks under concurrency.** Accepted for v0.1, documented, and the subject of the
  brownfield scenario.
- **`/{code}` catch-all shadowing other routes.** Covered by the health and OpenAPI tests.
- **Open redirect:** a shortener redirects to arbitrary URLs by design. Basic scheme rules now; hardened in AB-02.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | `LinkEndpoints` (create 201, details 200, redirect 302), `LinkContracts`, `ShortLinkOptions` (configured base URL, request host only as a fallback), ProblemDetails + exception handler + status code pages, OpenAPI + Scalar, `ShortLink.RegisterClick`, `LinkService.VisitAsync` (naive read-modify-write, commented as a v0.1 simplification), `ILinkRepository.UpdateAsync`, 16 new tests | Build caught an unused `using` in a test. 5 of 26 integration tests failed; see #2 and #3. |
| 2 | Why do the invalid-URL tests fail? | `ObjectDisposedException`: the test helper read the response body, then the test read it again | **Test bug, not an app bug.** Helper now reads the body once and returns it. |
| 3 | Why does malformed JSON return 500? | In Development, ASP.NET throws `BadHttpRequestException` for bad bodies; the exception handler turned it into a 500. Production would have returned 400, so the status depended on the environment | **App fix:** `ExceptionHandlerOptions.StatusCodeSelector` keeps the 400 of bad-request exceptions in every environment. |
| 4 | Smoke test the real app in Production mode | create 201, redirect 302 (count 0 → 1), bad URL 400 with reason, malformed JSON 400, unknown 404, `/scalar` 200, no stack traces | **Approved and signed off** (SIGNOFF #4). |

## Outcome
- **Decision:** Changed (test helper fix, 400 status fix)
- **AI-LOG row:** #8
- **Commit(s):** GF-04 commit
- **Sign-off:** SIGNOFF #4
