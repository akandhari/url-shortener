# AB-04: Expiry and disable (410 Gone)

| | |
|---|---|
| **Scenario** | Ambiguous ("make it safe from abuse") |
| **Depends on** | AB-01, BF-08 (redirect cache) |
| **Requirements** | FR-9, FR-4, NFR-3 |
| **Needs my sign-off?** | Reviewed with the scenario: schema, API contract, admin key |
| **Status** | In progress |

## Goal
A bad link can be taken down, and a creator can make a link expire, so abusive links don't live forever. Visitors
are told the link is gone (not that it never existed), and its analytics are kept.

## API changes (additive)
| Change | Detail |
|---|---|
| `POST /api/links` | Optional `expiresAt` (ISO 8601). Must be in the future and at most **365 days** ahead → otherwise **400** |
| `GET /{code}` | Expired or disabled → **410 Gone** (ProblemDetails, "expired" / "disabled"); no click recorded |
| `GET /api/links/{code}` | New fields: `expiresAt`, `disabledAt`, `status` (`active` / `expired` / `disabled`); still 200 for gone links |
| `DELETE /api/links/{code}` (new) | Disables the link. Header `X-Admin-Key` must match `Admin:ApiKey` → **204**; missing/wrong key → **401**; key not configured → **403** (endpoint off); unknown code → **404**; already disabled → 204 (idempotent) |

## Constraints
- Schema: two nullable columns on `Links` (`ExpiresAt`, `DisabledAt`), stored as UTC ticks like the other dates.
  Additive migration; v0.2 can still read the table.
- The admin key comes from configuration (environment variable `Admin__ApiKey` or user secrets), **never** from a
  file in the repo. Compared in constant time (`CryptographicOperations.FixedTimeEquals`).
- Redirect cache (BF-08): entries carry `ExpiresAt`, and the visit checks it against the current time, so a cached
  link still expires on time. Disabling **evicts** the entry. (Single instance; documented.)
- "Now" comes from `TimeProvider`.
- Web page: an "Expires" choice (never, 1 day, 7 days, 30 days) and the status shown with the result.

## Acceptance criteria
- [ ] Create with a past, too-far or malformed `expiresAt` → 400 with the reason.
- [ ] A link past its expiry → 410 on redirect, even when its target was cached before it expired; no click counted.
- [ ] Disable with the right key → 204, then the redirect → 410, even if it was cached; details show `disabled`.
- [ ] Wrong or missing key → 401; no key configured → 403; unknown code → 404.
- [ ] Stats still work for expired and disabled links.
- [ ] Upgrade: a v0.2 database migrates with existing links active.
- [ ] `scripts/verify.ps1` passes.

## Out of scope
- Re-enabling links, editing targets, per-user ownership, abuse-report endpoint.

## Risks
- **Shared admin key** is a stopgap until real authentication; it can be rotated through configuration.
- **Several instances:** one instance's cache eviction doesn't reach the others. Each holds an entry for at most
  5 minutes (cache TTL). Documented.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | `LinkStatus` + status rules, `ExpiresAt`/`DisabledAt`, `VisitResult` (redirect / gone / not found), expiry window check, `DisableAsync` (atomic `ExecuteUpdate`, cache invalidation), shared `UtcTicksConverter`, migration `AddExpiryAndDisable` (two nullable columns), `AdminKey` (constant-time compare), `DELETE /api/links/{code}`, `status`/`expiresAt`/`disabledAt` in details, page "Expires" choice | Simplified a convoluted status mapping (enum → string → upper case → match) to a plain `switch` before running it; removed an unused `using`. Moving existing dates to the shared converter changed nothing in the schema (confirmed by the generated migration). |
| 2 | Run the tests | Fakes needed the new interface members; `MigrationTests` listed 2 migrations, now 3; BF-09's upgrade test asserted the `Links` schema is identical | Updated deliberately: the upgrade test now asserts every v0.1 column is still there unchanged and old links stay active, which is the real rollback-safety claim (new columns are nullable and unselected by older versions). |
| 3 | Real app with an admin key | 302 → DELETE without key 401 → with key 204 → 410 "disabled", details `status: disabled`, no errors logged | Green: 132 unit, 75 integration. Pending scenario review. |

## Outcome
- **Decision:**
- **AI-LOG row:**
- **Commit(s):**
- **Sign-off:** scenario review
