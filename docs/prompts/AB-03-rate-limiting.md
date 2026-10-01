# AB-03: Per-client rate limits

| | |
|---|---|
| **Scenario** | Ambiguous ("make it safe from abuse") |
| **Depends on** | AB-01 |
| **Requirements** | FR-7, NFR-3 |
| **Needs my sign-off?** | Reviewed with the scenario: rate-limit thresholds (a policy decision) |
| **Status** | In progress |

## Goal
Slow down mass link creation and code enumeration from a single client, without affecting normal use, and tell the
client clearly when to retry.

## Context
- Threats 5 and 6 in `docs/scenarios/03-ambiguous.md`; ADR-0004.
- ASP.NET Core has a built-in rate limiter (`Microsoft.AspNetCore.RateLimiting`, part of the shared framework): no new package.

## Policies (defaults; all in configuration under `RateLimits`)
| Policy | Applies to | Default per client IP | Why |
|---|---|---|---|
| `create` | `POST /api/links` | **10 per minute** | Humans create a few links; scripts create thousands |
| `redirect` | `GET /{code}` | **300 per minute** | Generous for real traffic (shared offices); guessing 2.2 trillion codes at 5/s is hopeless |
| `lookup` | `GET /api/links/{code}`, `.../stats` | **60 per minute** | Slows scraping of link details |

Token bucket per client IP (bursts up to the limit, refilled each window). The web page, health checks and API docs
are not limited.

## Constraints
- Rejection: **429 Too Many Requests**, a `Retry-After` header (seconds), and a ProblemDetails body.
- Limits are configuration, not code, so ops can tune them without a deploy (open question 5 in the scenario doc).
- Client key = the connection's remote IP. **Behind a proxy this is the proxy's IP**: documented; trusting
  `X-Forwarded-For` must be restricted to known proxies and is a deployment decision (open question 6).
- Tests that are not about rate limiting run with generous limits, so they never trip over them.

## Acceptance criteria
- [ ] The (N+1)-th create from one client within the window → 429 + `Retry-After` + ProblemDetails.
- [ ] Hitting the create limit does not block redirects or lookups (separate policies).
- [ ] The redirect and lookup limits trigger the same way.
- [ ] The web page and health endpoints are never limited.
- [ ] `scripts/verify.ps1` passes.

## Out of scope
- Distributed limits across instances (Redis), per-user limits (no accounts), CAPTCHA.

## Risks
- **Shared IPs** (offices, mobile carriers) share one budget: defaults are generous for redirects; tune with real data.
- **Per-instance limits:** with N instances a client gets N× the budget. Documented (ADR-0004).

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | Token-bucket policies `create` / `redirect` / `lookup` per client IP from configuration, 429 + `Retry-After` + ProblemDetails, policies attached per endpoint, test factory with generous defaults, 7 tests | Analyzer flagged an unused `using`. Green. |
| 2 | What if the config is half-filled? | A missing `PermitLimit` binds to 0, and the limiter would throw on the first request | **Added a startup check** with a clear message ("RateLimits:Create needs PermitLimit > 0 …"); verified by starting the app with a bad value. |
| 3 | Real app, default config | 10 × 201 then 429 with `Retry-After: 60` and ProblemDetails | Pending scenario review. |

## Outcome
- **Decision:**
- **AI-LOG row:**
- **Commit(s):**
- **Sign-off:** scenario review
