# AB-03b: Custom alias with abuse rules

| | |
|---|---|
| **Scenario** | Ambiguous ("make it safe from abuse") |
| **Depends on** | AB-02 |
| **Requirements** | FR-8, FR-4, NFR-3 |
| **Needs my sign-off?** | Reviewed with the scenario: API contract (new optional field, 409) and alias rules |
| **Status** | In progress |

## Goal
Let a creator choose a readable code (`/fall-sale`) without letting anyone hijack our routes or impersonate
sensitive pages (`/secure-login`).

## API change (additive)
`POST /api/links` accepts an optional `alias`: `{ "url": "https://example.com/sale", "alias": "fall-sale" }`.
| Case | Result |
|---|---|
| Valid, free alias | **201**, `code` = the alias |
| Alias breaks the rules | **400** with the reason |
| Alias already taken | **409 Conflict** (no silent fallback to a random code) |
| No alias | Unchanged: random 7-character Base58 code |

## Alias rules
| Rule | Why |
|---|---|
| Letters, digits and single hyphens; must start and end with a letter or digit (`fall-sale`, not `-x`, `a--b`) | Readable and unambiguous in a URL |
| 4–32 characters | Short aliases are squatting targets; 32 is the `Code` column size (no migration needed) |
| Stored in **lower case** (`Fall-Sale` → `fall-sale`) | One alias can't exist in two cases that look the same to people |
| Reserved words rejected: `api`, `health`, `scalar`, `openapi`, `admin`, `static`, `assets`, `index`, `app`, `favicon`, `robots`, `sitemap` | Can't shadow our routes or look like our own pages |
| Impersonation words rejected as a hyphen-separated part: default `login`, `signin`, `password`, `verify`, `account`, `secure`, `wallet`, `support`; more via `Abuse:BlockedAliasWords` | Phishing aliases like `secure-login` or `verify-account`; matching whole parts avoids false hits like `bankholiday` |

## Constraints
- Rules in Core (`Alias`), configurable word list in `AliasPolicy`; no real company names in the repo's defaults.
- `ShortCode.IsWellFormed` accepts both formats, so redirect, details and stats work for aliases; malformed input
  still never reaches the database.
- The unique index on `Code` decides "taken" (no check-then-insert).
- The web page gets an optional alias field.

## Acceptance criteria
- [ ] Rule matrix: accepted and rejected aliases, each rejection with its reason.
- [ ] Create with alias → 201 and the redirect, details and stats work for it.
- [ ] Same alias again → 409; mixed case is stored lower case.
- [ ] Reserved and impersonation aliases → 400.
- [ ] Without an alias, behaviour is unchanged.
- [ ] `scripts/verify.ps1` passes.

## Risks
- **Word lists are never complete.** Mitigation: configurable, and reputation checks are the real defence (deferred).
- **An alias like `abc1234` looks like a generated code.** Harmless: uniqueness is enforced by the database; a
  generated code that collides is simply retried.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | `AliasRules` + `AliasPolicy` (source-generated regex, reserved words, whole-part word match), `CreateAsync(url, alias)` with 400/409, `ShortCode.IsWellFormed` = generated OR alias shape, optional field on the page | Wrote `ToUpperInvariant().ToLowerInvariant()` a third time to dodge CA1308; checked: **the rule doesn't fire**, so it was never needed. Plain `ToLowerInvariant`. Analyzer CA1716: `Alias` is a VB keyword → `AliasRules`. |
| 2 | Run the tests | Almost every integration test failed at start-up: `TypeInitializationException` in `AliasPolicy` | **Static fields initialise in written order:** `Default = new(...)` was declared before the word list it reads, so the list was null. Moved the list first, with a comment. 5 unit tests failed as expected: inputs like `abc-123` were "malformed", now valid alias shapes; split into `IsGenerated` vs `IsWellFormed` tests. |
| 3 | Alias tests | One failure: `api` returned the length message, not "reserved" | Test data, not a bug: 3-character words are already stopped by the length rule. Tested the reserved rule with `admin`. Green: 125 unit, 67 integration. Pending scenario review. |

## Outcome
- **Decision:**
- **AI-LOG row:**
- **Commit(s):**
- **Sign-off:** scenario review
