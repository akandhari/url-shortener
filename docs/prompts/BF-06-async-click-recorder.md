# BF-06: Async click recorder (bounded queue + background writer)

| | |
|---|---|
| **Scenario** | Brownfield (CR-002) |
| **Depends on** | BF-05 |
| **Requirements** | FR-5, NFR-1, NFR-2, NFR-4 |
| **Needs my sign-off?** | Reviewed with the scenario (no new packages; behaviour change: click count becomes eventually consistent) |
| **Status** | Done |

## Goal
Every click is counted, even under concurrent redirects (BF-03 turns green), and the visitor never waits for a
database write. Each click becomes a `ClickEvent` row that later powers per-day and per-referrer stats.

## Context
- `docs/scenarios/02-brownfield.md` section 3: impact analysis and target design.
- BF-03 (`ConcurrentClickTests`) fails today: 50 concurrent redirects → count 1.
- BF-04 added the `IClickRecorder` seam; BF-05 added the `ClickEvents` table.

## Constraints
- **No read-modify-write anywhere.** Events are inserted; `Links.ClickCount` is increased inside the database
  (`ClickCount = ClickCount + n`), never read, changed in memory and written back.
- The redirect only **enqueues** (non-blocking). If the queue is full, the click is **dropped and counted**, and a
  warning is logged; the redirect is never slowed or failed by analytics.
- **Bounded** queue (default 10,000 events): an unbounded queue could grow until the process runs out of memory.
- One background writer drains the queue in batches (up to 500 events per transaction). Batches form naturally under
  load; no fixed delay, so counts appear within milliseconds when idle.
- On graceful shutdown the writer **drains what is left** before exiting.
- A failed batch is logged and skipped; the writer keeps running.
- Referrer: only the **host** of an absolute http(s) `Referer` header, lower-cased; anything else → no referrer.
  No IP address, no full referrer URL.
- No new packages (`System.Threading.Channels` is part of .NET).
- Remove what becomes dead code: `ReadModifyWriteClickRecorder`, `ILinkRepository.UpdateAsync`, `ShortLink.RegisterClick`.

## Acceptance criteria
- [ ] BF-03 `Concurrent_redirects_are_all_counted` passes (50/50), repeatedly.
- [ ] Characterization tests (BF-02) still pass unchanged.
- [ ] Each click stores link, time (UTC) and referrer host; `ClickCount` matches the number of events.
- [ ] Full queue → click dropped, drop counted, no exception.
- [ ] Stopping the host flushes queued events to the database.
- [ ] The GF-04 test that read `clickCount` immediately after a redirect now waits for it (documented behaviour change).
- [ ] `scripts/verify.ps1` passes.

## Test plan
- Integration: BF-03 and BF-02 (unchanged), `ClickRecordingTests` (events + atomic counts, referrer host, shutdown flush),
  queue-full behaviour.
- Unit: referrer host extraction; `LinkService.VisitAsync` records an event with the right link, time and referrer.

## Out of scope
- Stats endpoint (BF-07), lookup cache (BF-08), durable queue (Service Bus/Kafka; documented as the production path).

## Risks
- **Clicks lost on a crash** (events still in memory). Accepted for analytics; documented.
- **Counts lag briefly** behind redirects (eventual consistency). Documented; tests wait for the outcome.
- **SQLite single writer:** one background writer avoids competing writers from request threads.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | `ClickBuffer` (bounded channel, counts drops), `QueuedClickRecorder`, `ClickWriterService` (batches: insert events + `ExecuteUpdate` `ClickCount = ClickCount + n` in one transaction; drains on shutdown), referrer host extraction, `VisitAsync(code, referrer)`; dead code removed | Three fixes during the build: the background writer needs `Microsoft.Extensions.Hosting.Abstractions` (**new package, flagged for sign-off**; kept in Infrastructure rather than moving the writer to Api); analyzer CA1711 rejected a type named `ClickQueue` → `ClickBuffer`; a pointless `ToUpperInvariant().ToLowerInvariant()` replaced (Uri already lower-cases hosts). |
| 2 | Run the gates | **BF-03 green: 50/50**, 5 out of 5 repeated runs. All characterization tests unchanged and green. 53 unit + 41 integration tests | The GF-04 test that read `clickCount` right after a redirect now waits for it: the documented behaviour change (eventually consistent), not a weakened test. |
| 3 | (found during BF-07) Shutdown-flush test failed once in a full run | With a capturing logger and repeated runs: in .NET 10, `BackgroundService` starts `ExecuteAsync` on a background task, so if the host stops before it begins, the "drain on cancel" code in `ExecuteAsync` never runs and queued clicks are lost | **Real bug, fixed:** drain in `StopAsync` after the base class has stopped the loop. The test now also asserts no warnings/errors were logged. 15/15 alone, 4/4 full-suite runs green. Committed separately from BF-07. |

**Process note:** the planned "AI draft → my edits" two-commit showcase is dropped for this scenario. With one review
at the end of the scenario (agreed to meet the deadline) there are no mid-story edits by me to show, and I won't fake them.

## Outcome
- **Decision:** Changed (see iterations)
- **AI-LOG row:** #14, #15
- **Commit(s):** see PR #2
- **Sign-off:** SIGNOFF #8, #9, approved at scenario review (PR #2)
