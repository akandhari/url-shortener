# ADR-0002: Short code generation (random Base58, 7 characters)

- **Status:** Accepted (GF-02)
- **Date:** 2026-09-30

## Context
Every link needs a short code that is short enough to type, hard to guess, and unique. Codes are read aloud,
printed and typed by people, not only clicked.

## Decision
Generate **7 random characters from the Base58 alphabet**
(`123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz`) using
`RandomNumberGenerator.GetString`, and let the database's unique index reject the rare duplicate; the service then
retries with a new code (up to 5 attempts).

## Options considered
| Option | Codes with 7 chars | Why not |
|---|---|---|
| **Base58 (chosen)** | 58^7 ≈ 2.2 trillion | Leaves out `0 O I l`, which look alike when read or typed |
| Base62 | 62^7 ≈ 3.5 trillion | More codes, but includes the look-alike characters |
| Hex | 16^7 ≈ 268 million | Far fewer codes; needs ~11 characters to match |
| Base64Url | 64^7 ≈ 4.4 trillion | Codes contain `-` and `_`, awkward at the end of URLs |
| Sequential counter → Base58 | unlimited | Guessable: anyone can walk through every link |
| Hash of the URL | depends | Same URL → same code (against our requirement); truncated hashes collide |
| `random byte % 58` by hand | 2.2 trillion | Modulo bias: 256 isn't divisible by 58, so some characters appear more often |

Precedent for Base58: Bitcoin addresses (designed for manual copying) and Flickr's `flic.kr` short links.

## Consequences
- ✅ Unpredictable codes (cryptographically secure source), no look-alike characters, no extra packages.
- ✅ Collisions are handled by the database, not assumed away. With 2.2 trillion codes there is a ~50% chance of
  *some* collision only after about 1.75 million links, and each new code's own chance of colliding stays tiny;
  the retry covers it.
- ⚠️ Codes are case-sensitive (`abc` ≠ `ABC`); the database uses case-sensitive comparison to match.
- ➡️ The format rules live in `ShortCode`, separate from the generator, so custom aliases (AB-03b) can extend the
  rules without touching generation.
