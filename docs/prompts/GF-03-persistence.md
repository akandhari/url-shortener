# GF-03: Persistence with EF Core and SQLite

| | |
|---|---|
| **Scenario** | Greenfield |
| **Depends on** | GF-02 |
| **Requirements** | FR-1, FR-2, FR-3, NFR-2, NFR-6 |
| **Needs my sign-off?** | Yes: schema (first migration) · dependencies (EF Core packages + `dotnet-ef` tool) |
| **Status** | Done |

## Goal
Links survive a restart. `ILinkRepository` from GF-02 gets a real implementation on SQLite through EF Core,
with code uniqueness guaranteed by the database itself, and the API reports whether the database is reachable.

## Context
- Core defines `ShortLink` and `ILinkRepository` (`TryAddAsync` returns false when a code is taken; `FindByCodeAsync`).
- `LinkService` retries with a new code when `TryAddAsync` returns false.
- Decided in `docs/REQUIREMENTS.md`: SQLite, single node, no external services (NFR-6).
- `.gitignore` already excludes `*.db`, `*.db-shm`, `*.db-wal`.

## Constraints
- Packages (need sign-off):
  - `Microsoft.EntityFrameworkCore.Sqlite` in Infrastructure;
  - `Microsoft.EntityFrameworkCore.Design` for migrations (development-time only, `PrivateAssets=all`);
  - `dotnet-ef` as a **local** tool (`.config/dotnet-tools.json`), so everyone uses the same version.
- No health-check package: the database check is a few lines of our own code.
- Core stays free of EF Core: mapping lives in Infrastructure (`IEntityTypeConfiguration<ShortLink>`), not as attributes.
- Uniqueness comes from a **unique index**, never from "check whether it exists, then insert" (that races).
- The duplicate-code case is detected from SQLite's unique-constraint error, and the failed entity is detached
  so the same `DbContext` can be used for the retry.
- `TargetUrl` is stored as text (max 2048). `Code` max length 32, to leave room for custom aliases in AB-03b.
- `CreatedAt`: SQLite has no native date-time-offset type, and EF Core cannot sort or compare `DateTimeOffset`
  stored as text there. Store it as UTC ticks (a number) with a value converter, so date grouping works in BF-07.
- Migrations are applied on startup. That's fine for a single-node prototype; in production they'd be a separate
  deployment step (documented as a trade-off).
- Connection string in `appsettings.json` (`Data Source=urlshortener.db`), with no secrets.

## Design
| Type | Responsibility |
|---|---|
| `AppDbContext` | `DbSet<ShortLink> Links`, applies configurations |
| `ShortLinkConfiguration` | Table `Links`, key `Id`, unique index on `Code`, conversions for `TargetUrl` and `CreatedAt` |
| `EfLinkRepository` | `ILinkRepository` on EF Core; returns false on a unique violation |
| `DatabaseHealthCheck` | `CanConnectAsync` → Healthy / Unhealthy |
| `AddInfrastructure(...)` | Registers DbContext, repository, generator, `TimeProvider.System`, `LinkService` |
| Migration `InitialCreate` | Creates `Links` with the unique index |

## Acceptance criteria
- [ ] Round trip: a saved link is found by its code with the same target, created time and click count.
- [ ] Saving a second link with an existing code returns false, stores nothing, and the next save on the same
      context succeeds.
- [ ] `LinkService` end-to-end on real SQLite: a forced collision is retried and succeeds.
- [ ] Unknown code returns null.
- [ ] Migrations apply to an empty database, and the model has **no pending changes** compared with the
      migrations (catches a forgotten migration).
- [ ] `GET /health/ready` returns 200 when the database is reachable. `/health/live` does not touch the database.
- [ ] Running the API creates `urlshortener.db` on first start, and nothing new shows up in `git status`.
- [ ] `scripts/verify.ps1` passes.

## Test plan
- Integration: `EfLinkRepositoryTests` on SQLite **in-memory** (one open connection per test), so tests use the
  real SQL engine and real constraints, not a fake.
- Integration: `MigrationTests` (apply + no pending model changes).
- Integration: `HealthEndpointTests.Ready_returns_200` via `WebApplicationFactory`, with the connection string
  overridden to in-memory SQLite.

## Out of scope
- HTTP endpoints for links (GF-04), click counting (GF-04), caching (BF-08), Postgres.

## Risks
- **SQLite single writer:** fine for the prototype; writes are short. Recorded in ADR-0001 as the reason a real
  deployment would move to Postgres (the repository interface makes that a contained change).
- **Migrate-on-startup in production:** could race across instances. Documented; single node only here.
- **Error detection tied to SQLite error codes:** isolated in one method, with a test that proves it works.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | `AppDbContext`, `ShortLinkConfiguration` (unique index on `Code`, `CreatedAt` as UTC ticks), `EfLinkRepository` (detects SQLite unique violation 19/2067, detaches the failed entity), design-time factory, `AddInfrastructure`, `MigrateDatabaseAsync`, `DatabaseHealthCheck`, `InitialCreate` migration, 9 integration tests on in-memory SQLite | Two deviations from the spec, both kept: (a) Core services (`LinkService`, generator, `TimeProvider`) are registered in `Program.cs`, not `AddInfrastructure`, because they are not infrastructure; (b) the health check lives in the Api project, so Infrastructure needs no ASP.NET package. Migrations marked `generated_code` in `.editorconfig`. |
| 2 | Prove the "forgotten migration" test bites | Changed `Code` max length 32 → 64: the test still **passed**. Probed with `dotnet ef migrations add`: EF generated an **empty** migration, because SQLite does not enforce text length. Renaming the table made the test fail as expected | Test is sound; max length is only documentation on SQLite. Noted as a limitation: on Postgres, length becomes real. Probe migration removed. |
| 3 | Run the real app | `/health/ready` and `/health/live` return 200; `urlshortener.db` created in the Api folder and ignored by git | **Approved and signed off** (SIGNOFF #3). |

## Outcome
- **Decision:** Changed (service registration and health check placement)
- **AI-LOG row:** #7
- **Commit(s):** GF-03 commit
- **Sign-off:** SIGNOFF #3
