# BF-07: Stats endpoint and stats panel

| | |
|---|---|
| **Scenario** | Brownfield (CR-002) |
| **Depends on** | BF-06 |
| **Requirements** | FR-5, FR-4, NFR-4 |
| **Needs my sign-off?** | Reviewed with the scenario: public API contract (additive) |
| **Status** | Done |

## Goal
Answer the change request: for one link, how many clicks per day and from which sites, available over the API and on
the web page.

## API contract (additive; nothing existing changes)
`GET /api/links/{code}/stats` → **200**, or **404** ProblemDetails for an unknown code.
```json
{
  "code": "k3X9aQ2",
  "totalClicks": 42,
  "clicksPerDay": [ { "date": "2026-09-30", "clicks": 12 }, { "date": "2026-10-01", "clicks": 30 } ],
  "topReferrers": [ { "host": "news.example.com", "clicks": 25 }, { "host": "(direct)", "clicks": 17 } ]
}
```
- `clicksPerDay`: **UTC** days, the **last 30 days** including today, oldest first, only days with clicks.
- `topReferrers`: top **5** hosts over all time, most clicks first; no referrer → `"(direct)"`.
- `totalClicks`: the link's `ClickCount`. For links created before v0.2 it includes clicks counted before click events
  existed, so it can be higher than the sum of `clicksPerDay` (documented, not a bug).

## Constraints
- The query is a read model in Infrastructure behind a Core interface (`ILinkStatsQuery`); Core stays free of EF Core.
- Grouping by day happens **in the database**. `OccurredAt` is stored as UTC ticks, so a day is `ticks / ticksPerDay`;
  EF Core can't do arithmetic on a value-converted property, so this one query is parameterised SQL (`SqlQuery`,
  interpolated values become parameters, no string concatenation).
- "Now" comes from `TimeProvider` (testable).
- Web page: a stats panel under the result (per-day list and top referrers), refreshed with the existing Refresh
  button; built with `createElement` + `textContent` only (no `innerHTML`, CSP unchanged).

## Acceptance criteria
- [ ] Known events (several days, inside and outside the 30-day window, several referrers including none) produce
      exactly the expected per-day and referrer numbers.
- [ ] Unknown code → 404 ProblemDetails; malformed code → 404 without a database query.
- [ ] The OpenAPI document lists the new endpoint.
- [ ] The page shows the stats after shortening and after Refresh.
- [ ] `scripts/verify.ps1` passes.

## Test plan
- Integration: `LinkStatsTests`: seed click events with known dates and referrers directly in the database, call the
  endpoint, assert the exact JSON; 404 cases; OpenAPI lists the route.
- Unit: `LinkService.GetStatsAsync`: unknown/malformed → null; the 30-day window is computed from `TimeProvider`.
- Manual: page panel in a browser.

## Out of scope
- Time zones other than UTC, charts, export, bot filtering, retention policy.

## Risks
- **Large links make the query slow.** Mitigation: index `(LinkId, OccurredAt)` from BF-05; the window is bounded.
- **Raw SQL bypasses EF's mapping checks.** Mitigation: one small query, parameterised, covered by an exact-numbers test.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | `ILinkStatsQuery` (Core) + `EfLinkStatsQuery` (per-day via parameterised `SqlQuery`, referrers via LINQ), `LinkService.GetStatsAsync`, `GET /api/links/{code}/stats`, `LinkStatsResponse` (null host → `(direct)`), stats panel on the page (`createElement` + `textContent`), 7 tests | Analyzer CA1305 caught a culture-dependent date format in a test → invariant culture. |
| 2 | Run the gates | 3 failures: the stats endpoint returned **500**, and the BF-06 shutdown test failed once | 500: EF Core can't translate ordering on a record built inside the query; fixed by sorting an anonymous projection and building the records in memory (found from the server log, not guessed). Shutdown test: a real BF-06 bug, see BF-06 iteration 3. |
| 3 | Re-run | All gates green (56 unit, 46 integration); full suite 4/4 | Approved at scenario review (PR #2). |

## Outcome
- **Decision:** Changed (see iterations)
- **AI-LOG row:** #16
- **Commit(s):** see PR #2
- **Sign-off:** SIGNOFF #7, approved at scenario review (PR #2)
