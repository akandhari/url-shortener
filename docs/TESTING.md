# Testing

## 1. Approach
| Layer | What it proves | How |
|---|---|---|
| **Unit** (132 test cases) | Business rules in Core: codes, aliases, URL abuse rules, expiry, `LinkService` | Fakes for repository, recorder, lookup and clock; no I/O; runs in under 0.1 s |
| **Integration** (83 test cases) | The real app end to end: HTTP status codes, headers, JSON shapes, routing, rate limits, persistence, migrations, background writer | `WebApplicationFactory` starts the API in memory; **real SQLite** (in memory) with the real migrations, so constraints behave as in production |
| **Load** | Redirect latency and click-recording capacity | k6 in Docker against the container ([perf/redirect-load.js](../perf/redirect-load.js)) |
| **Manual** | What automation doesn't cover | Real app in Production mode per scenario; web page in a browser; container run |

Run everything: `./scripts/verify.ps1` (the same script CI runs).

## 2. Techniques used on purpose
| Technique | Where | Why |
|---|---|---|
| **Characterization tests** | BF-02 | Pin existing behaviour before changing the code underneath; never edited afterwards |
| **Red first** | BF-03 | Proved the lost update (1 of 50 clicks counted) before fixing it (50 of 50) |
| **Table-driven attack matrices** | AB-02 (38 cases), AB-03b (21 cases) | Every abuse case and its expected reason in one readable table |
| **Mutation checks** (break the code, expect red) | GF-01, GF-03, GF-05, AB-02 | Prove tests can fail: planted warning, table rename without migration, naive middleware order, one IP rule removed |
| **Exact-number tests** | BF-07 | Seed clicks on known days and referrers, assert exact stats |
| **Repeated runs for concurrency** | BF-03, BF-06 | 5×, 15× and full-suite repeats before trusting a concurrency fix |
| **Outcome over timing** | BF-02 onward | Poll until the count is reached, so tests hold for eventually consistent counts without sleeps |
| **Upgrade test** | BF-09 | A v0.1 database migrates with data intact and v0.1 columns unchanged |
| **Logs as assertions** | BF-06, WR-02 | Writer must log no warnings in a clean shutdown; drop warnings are throttled |

## 3. Results
- **All green** locally and in CI on every pushed commit: 132 unit + 83 integration test cases.
- **Line coverage 93.5%** (Api 97.6%, Infrastructure 96.6%, Core 89.9%); generated migrations excluded.

## 4. Load test (WR-02)
Setup: the container on a laptop (Docker Desktop), k6 with 50 virtual users for 30 seconds against `GET /{code}`, all
from one IP. The redirect rate limit was raised **through configuration** for the run
(`RateLimits__Redirect__PermitLimit`); otherwise the abuse protection (300/min per IP) would answer most requests
with 429, which measures the limiter rather than the redirect.

| Measure | Result |
|---|---|
| Throughput | ~6,400 redirects per second |
| Latency | p95 **12.3 ms**, median 7 ms |
| Errors | **0%** (all 302) |
| Click recording | One SQLite writer stores **~4,000 clicks/s**; above that the bounded buffer fills and clicks are **dropped and counted**, never slowing or failing the redirect |

The first run showed **stored + dropped + still buffered = all redirects**: 121,028 + 63,570 + ~10,300 = 194,921,
so no click was unaccounted for.

**Findings and actions**
1. **Log flooding (fixed):** one warning per dropped click produced 63,570 log lines in 30 seconds. Now the first drop
   and every 1,000th are logged (66 lines for the same load), with a test.
2. **Batch size is not the bottleneck:** batches of 500, 2,000 and 5,000 all stored ~4,000 clicks/s, so the default
   stays at 500. Higher sustained click rates need a durable queue or bulk inserts on PostgreSQL (ADR-0003).
3. **In context:** with the default limits, a single client can't cause drops; it takes many clients at more than
   ~4,000 clicks/s sustained.

## 5. Docker smoke test (WR-01)
`docker build` → `docker run -p 8088:8080 -v urlsh-data:/data`:
- `/health/ready` 200;
- runs as the non-root `app` user (uid 1654);
- create with alias 201;
- redirect 302 with the referrer recorded;
- page 200;
- stats correct;
- cloud-metadata target refused (400);
- **clicks still there after `docker restart`** (SQLite on the volume);
- no errors in the logs.

## 6. Limitations of the testing
- No automated JavaScript tests for the web page; it is small and was checked by hand in a browser.
- Tests use SQLite; a PostgreSQL deployment would need the integration tests run against PostgreSQL too.
- The load test ran on a laptop from one IP; production numbers depend on hardware and on a durable queue.
- No soak test (hours of load) and no chaos testing (killing the process mid-batch); the documented behaviour is
  "buffered clicks are lost on a crash".
