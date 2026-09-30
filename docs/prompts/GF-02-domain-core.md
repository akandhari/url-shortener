# GF-02: Domain core (short link, code generator, URL validation, link service)

| | |
|---|---|
| **Scenario** | Greenfield |
| **Depends on** | GF-01 |
| **Requirements** | FR-1, FR-2, FR-6 (basic), NFR-3, NFR-5 |
| **Needs my sign-off?** | No (no schema, API or package changes) |
| **Status** | Done |

## Goal
The business logic for creating and resolving short links, in `UrlShortener.Core`, with no framework
dependencies, so it can be tested fast and in isolation. Persistence (GF-03) and HTTP (GF-04) plug into it later.

## Context
- `UrlShortener.Core` is empty and has no package references; it must stay that way (see `CLAUDE.md`).
- Decisions already made in `docs/REQUIREMENTS.md`:
  - codes are 7 characters, cryptographically random;
  - alphabet is **Base58** (decided in this task, see below);
  - only absolute `http`/`https` URLs are accepted;
  - the same URL shortened twice gets a new code.

## Constraints
- No new packages. Use `RandomNumberGenerator.GetString` (built into .NET) for codes.
- Use `TimeProvider` for timestamps, so tests control the clock. No `DateTime.Now`.
- Invalid input is a normal outcome, not an exception: return a result the API can map to 400.
- Code uniqueness is enforced by the database later (GF-03). Core defines the contract: the repository reports
  a duplicate code, and the service retries with a new code a limited number of times.
- Click counting is **not** part of this task (GF-04).

## Design
| Type | Responsibility |
|---|---|
| `ShortLink` | Entity: `Code`, `TargetUrl`, `CreatedAt`, `ClickCount` |
| `ShortCode` | The code format: Base58 alphabet, length 7, `IsWellFormed(code)` |
| `ICodeGenerator` / `RandomCodeGenerator` | 7-character Base58 code from a CSPRNG |
| `TargetUrlValidator` | Basic rules: not empty, absolute URI, scheme http/https, has a host, max 2048 characters |
| `ILinkRepository` | `AddAsync` (reports a duplicate code), `FindByCodeAsync` |
| `LinkService` | `CreateAsync(url)` → validate → generate → store, retry on duplicate; `ResolveAsync(code)` → link or not found |
| `CreateLinkResult` | Success with the link, or a failure with an error code and message |

## Decision: Base58 alphabet
Short codes get read aloud, typed from print, and shown in fonts where `0`/`O` and `I`/`l` look the same.
Base58 (`123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz`) removes exactly those four characters.
Cost: 58^7 ≈ 2.2 trillion codes instead of 62^7 ≈ 3.5 trillion, which is still far beyond what this service needs
and still impractical to guess (with rate limiting in AB-03). Precedent: designed for Bitcoin addresses that people
copy by hand, and used by Flickr for `flic.kr` short links.
Rejected: hex (16^7 ≈ 268 million, too small), Base64Url (`-`/`_` in codes), sequential IDs (guessable),
hashing the URL (same URL → same code, and truncated hashes collide), `bytes % 62` by hand (modulo bias).

## Acceptance criteria
- [ ] Generator: always 7 characters, only Base58 characters (never `0`, `O`, `I`, `l`); 10,000 generated codes have no duplicates.
- [ ] Validator accepts `http`/`https` absolute URLs and rejects: empty, whitespace, relative, `ftp:`, `javascript:`,
      `mailto:`, no host, and more than 2048 characters.
- [ ] `CreateAsync` with a valid URL stores and returns a link with a 7-character code and `CreatedAt` from `TimeProvider`.
- [ ] `CreateAsync` with an invalid URL returns a failure and stores nothing.
- [ ] A duplicate code is retried with a new code; after 5 duplicates in a row it gives up with a clear failure.
- [ ] `ResolveAsync` returns the link for a known code and "not found" for an unknown or malformed code,
      without hitting the repository for malformed codes.
- [ ] `UrlShortener.Core.csproj` still has no package references.
- [ ] `scripts/verify.ps1` passes.

## Test plan
- Unit: `RandomCodeGeneratorTests`, `TargetUrlValidatorTests` (table-driven), `LinkServiceTests` with an in-memory
  fake repository and a fixed-time `TimeProvider`.

## Out of scope
- Database, HTTP endpoints, click counting, hardened security rules (private IPs etc. come in AB-02), custom alias.

## Risks
- **Too-strict validation rejects real URLs** (e.g. internationalised domains). Mitigation: test a few real-world
  shapes, and the hardened rules get their own matrix in AB-02.
- **Retry loop hides a broken generator.** Mitigation: the retry cap plus a test for the give-up path.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Which alphabet? AI proposed Base62 | Compared hex, char ranges, Base64Url, `% 62` by hand, counters, hashing | **Changed:** I chose Base58 (no look-alike characters) for human readability; reasoning recorded above. Becomes ADR-0002 in GF-06. |
| 2 | Build to the spec | `ShortCode` (format), `RandomCodeGenerator`, `ShortLink`, `TargetUrlValidator`, `ILinkRepository` (`TryAddAsync` returns false on a taken code), `CreateLinkResult`, `LinkService`, 44 unit tests. Two fixes during the build: a nullability hint on `IsWellFormed`, and an analyzer warning (CA1861) in one test assertion | **Approved.** Reviewed the split between `ShortCode` (format rules) and `RandomCodeGenerator` (creation) before accepting. |

## Outcome
- **Decision:** Changed (Base58 instead of the proposed Base62)
- **AI-LOG row:** #5
- **Commit(s):** GF-02 commit
- **Sign-off:** N/A
