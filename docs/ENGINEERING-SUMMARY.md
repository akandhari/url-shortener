# Engineering summary

A URL shortener built in three stages (greenfield, brownfield, ambiguous) with AI assistance under explicit
engineering control. This page is the short version; every claim links to the evidence.

## 1. Plan and rationale
| Stage | Version | Why this shape |
|---|---|---|
| Requirements first | GF-00 | Turn the brief into FRs, NFRs and stated assumptions before any code ([REQUIREMENTS.md](REQUIREMENTS.md)) |
| Greenfield: core service | v0.1.0 | Smallest complete product: create, redirect, details, web page, with quality gates from the first commit ([scenario 1](scenarios/01-greenfield.md)) |
| Brownfield: click analytics | v0.2.0 | A real change request against my own frozen v0.1: impact analysis, characterization tests, a bug proven red before the fix ([scenario 2](scenarios/02-brownfield.md)) |
| Ambiguous: "make it safe from abuse" | v0.3.0 | Interpret first (threat model), build what the app can enforce itself, defer the rest with reasons ([scenario 3](scenarios/03-ambiguous.md)) |

Each scenario: its own branch, one commit per task, a pull request with green CI, my sign-off in the merge commit, and a tag.

## 2. Artifacts
| Artifact | Where |
|---|---|
| Working prototype | `dotnet run --project src/UrlShortener.Api` or Docker ([README](../README.md)) |
| Architecture (components, tools, execution approach, control flow, decisions) | [ARCHITECTURE.md](ARCHITECTURE.md), [ADRs](adr/) |
| Three scenarios (decomposition, execution, validation) | [scenarios/](scenarios/) |
| Testing approach, results, limitations | [TESTING.md](TESTING.md) |
| Risks and trade-offs | [RISKS.md](RISKS.md), ADR consequences |
| Requirement → task → test | [TRACEABILITY.md](TRACEABILITY.md) |
| AI usage: policy, task specs, decisions, sign-offs | [AI-USAGE-POLICY.md](AI-USAGE-POLICY.md), [prompts/](prompts/), [AI-LOG.md](AI-LOG.md), [SIGNOFF.md](SIGNOFF.md), [CLAUDE.md](../CLAUDE.md) |
| API description | `/openapi/v1.json`, `/scalar`, [http/UrlShortener.http](../http/UrlShortener.http) |

## 3. How AI was used, and controlled
- **11 task specs** written before the code they produced; each records the AI's output and my response per iteration.
- **28 AI-LOG entries:** 12 kept, 15 changed, 1 rejected. "Changed" dominates because review and the gates found
  something to fix in most non-trivial tasks.
- **15 sign-offs** for schema, API contract, security, dependencies and CI changes; scenario merges carry my
  `Signed-off-by`.
- **Gates caught AI output that was wrong**, including:
  - a false claim about analyzer setup;
  - a 500 instead of 400 for malformed JSON;
  - an EF Core query that could not be translated;
  - a shutdown race in the background writer;
  - a static initialisation-order bug;
  - a config edge case that would crash at the first request;
  - one log line per dropped click (found by the load test).

  Each was diagnosed from evidence (failing test, server log, probe), not by guessing.
- **I overruled or redirected the AI** when it proposed staying on old package versions, Base62 instead of Base58,
  and an unreadable test. I also stopped a habit of working around an analyzer rule that wasn't firing.
- No secrets or employer material in prompts or the repo; gitleaks checks the full history.

## 4. Validation
| What | Result |
|---|---|
| Automated tests | 132 unit + 83 integration, green locally and in CI on every pushed commit |
| Line coverage | 93.5% (Api 97.6%, Infrastructure 96.6%, Core 89.9%), migrations excluded |
| Bug proven before fix | Lost update: 1 of 50 concurrent clicks counted → 50 of 50 after BF-06, stable over repeated runs |
| Tests proven able to fail | Planted warning fails the build; table rename without migration fails; naive middleware order fails page tests; removing one IP rule fails exactly its matrix cases |
| Upgrade safety | v0.1 database → latest: data intact, v0.1 columns unchanged |
| Load (k6, container, laptop) | ~6,400 redirects/s, p95 12.3 ms, 0% errors; one SQLite writer stores ~4,000 clicks/s, the rest dropped and counted ([TESTING.md](TESTING.md#4-load-test-wr-02)) |
| Container | Non-root, health checks, data persists across restart on a volume |
| Manual | Real app in Production mode for every scenario (create, redirect, stats, 429, 410, error bodies, no stack traces); web page in a browser |

## 5. Key trade-offs
| Chose | Over | Because |
|---|---|---|
| SQLite | PostgreSQL | Runs anywhere with no setup; repository keeps the swap contained |
| Eventually consistent click counts | Synchronous counting | Fast redirect, no lost updates; brief lag accepted |
| In-memory click buffer | Durable queue | No external infrastructure for a prototype; loss on crash accepted for analytics |
| App-level rate limits per IP | Proxy/CDN limits | Available on a laptop; proxy limits are the production layer |
| Shared admin key | Accounts and roles | Stopgap that is off by default and never stored in the repo |
| 302 | 301 | Every click reaches us |

## 6. Assumptions
The full list is in [REQUIREMENTS.md](REQUIREMENTS.md#ambiguities-and-my-assumptions) and the scenario docs. The main ones:
- anyone may create links (no accounts);
- a new code per request;
- UTC days;
- referrer host only;
- single instance;
- rate limits are starting guesses, kept in configuration.

## 7. Limitations
- No authentication or link ownership; stats are visible to anyone with the code.
- No URL reputation checks; host names that resolve to private IPs are not caught.
- Single instance: buffer, cache and rate limits are per process; buffered clicks are lost on a crash.
- Behind a proxy, rate limits need trusted forwarded headers.
- No automated JavaScript tests for the web page (checked manually).
- Bots are counted; no retention policy for click data.

## 8. What I'd do next
1. Authentication and link ownership.
2. Destination reputation checks.
3. PostgreSQL, with migrations as a deploy step.
4. A durable click queue, and Redis for cache and rate limits.
5. Observability: structured logs, metrics for redirect latency, drops and 429s, and tracing.
