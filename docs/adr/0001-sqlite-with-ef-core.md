# ADR-0001: SQLite with EF Core for persistence

- **Status:** Accepted (GF-03)
- **Date:** 2026-09-30

## Context
The prototype must run end-to-end on a reviewer's machine with no external services (NFR-6), while still showing
real database behaviour: unique constraints, migrations, and concurrency.

## Decision
Use **SQLite** through **EF Core 10**, behind the Core interface `ILinkRepository`.
- Code uniqueness is enforced by a **unique index** in the database, never by "check, then insert".
- The schema evolves only through **EF Core migrations**; a test fails if the model changes without one.
- Migrations are applied **on startup**.

## Options considered
| Option | Why not (for this prototype) |
|---|---|
| In-memory store | Loses data on restart; no real constraints or migrations to demonstrate |
| PostgreSQL / SQL Server | The right choice for production, but needs Docker or an install for every reviewer |
| Dapper + hand-written SQL | More code for no benefit at this size; EF Core gives migrations and a tested mapping |

## Consequences
- ✅ `dotnet run` just works; tests use the real SQLite engine in memory, so constraints behave as in the app.
- ⚠️ **Single writer:** SQLite serialises writes. Fine for a prototype, not for high write volume.
- ⚠️ **Text length is not enforced** by SQLite (`HasMaxLength` is documentation only); found while testing GF-03.
  Input limits are enforced in code (URL ≤ 2048).
- ⚠️ **Migrate on startup** would race with several instances. In production, run migrations as a deploy step.
- ➡️ Moving to PostgreSQL is a contained change: a new provider and migrations in Infrastructure; Core and Api don't change.
