# ADR-0003: Record clicks asynchronously as events

- **Status:** Accepted (CR-002, BF-06)
- **Date:** 2026-10-01

## Context
v0.1 counted clicks with a read-modify-write on the link row inside the redirect request. A test with 50 concurrent
redirects recorded **1** click (lost updates), every visitor waited for a database write, and a single counter can't
answer "clicks per day" or "from which site" (CR-002).

## Decision
- Each click becomes an append-only **`ClickEvent`** (link, UTC time, referrer host).
- The redirect puts the event into a **bounded in-memory buffer** (`System.Threading.Channels`, 10,000 events) and
  returns immediately.
- **One background writer** drains the buffer in batches (up to 500): it inserts the events and runs
  `UPDATE Links SET ClickCount = ClickCount + n` in the same transaction.
- If the buffer is full, the click is **dropped, counted and logged**; the redirect is never slowed or failed.
- On graceful shutdown the writer **drains** what is left.

## Options considered
| Option | Why not |
|---|---|
| Atomic SQL increment in the request (`ClickCount = ClickCount + 1`) | Fixes lost updates, but the visitor still waits for a write, and there is no per-click data for CR-002 |
| Insert one `ClickEvent` per request, synchronously | Correct and gives per-click data, but every redirect waits for a write; with SQLite's single writer, concurrent redirects queue up on the database lock |
| Unbounded in-memory queue | A burst could grow memory without limit |
| Durable queue (Service Bus, Kafka, Redis Streams) | The production answer (no loss on crash, several instances), but needs infrastructure the prototype must not require (NFR-6) |

## Consequences
- ✅ No lost updates: BF-03 went from 1/50 to 50/50, stable over repeated runs.
- ✅ The redirect does no database write; one writer avoids lock contention on SQLite.
- ✅ Per-day and per-referrer stats become simple queries over `ClickEvents`.
- ⚠️ **Eventual consistency:** counts appear shortly after the redirect (milliseconds when idle), not instantly.
- ⚠️ **Loss on crash:** events still in memory are lost if the process dies. Acceptable for analytics; normal shutdown drains.
- ⚠️ **Drops under extreme bursts** when the buffer is full; counted and logged so they are visible.
- ⚠️ **One instance only:** the buffer lives in one process. Several instances would need the durable queue above.
- 🐞 Found while testing: in .NET 10 `BackgroundService` starts `ExecuteAsync` on a background task, so the shutdown
  drain must live in `StopAsync`, not only in `ExecuteAsync`.
