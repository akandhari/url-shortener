# AB-02: Hardened target URL validation

| | |
|---|---|
| **Scenario** | Ambiguous ("make it safe from abuse") |
| **Depends on** | AB-01 |
| **Requirements** | FR-6, NFR-3 |
| **Needs my sign-off?** | Reviewed with the scenario: security rules |
| **Status** | In progress |

## Goal
Refuse to create links that deceive visitors, point into private networks, or loop through this service, with a
clear reason for each rejection.

## Context
- `TargetUrlValidator` (GF-02) already checks: required, ≤ 2048 characters, absolute, http/https, has a host.
- Threats 2–4 in `docs/scenarios/03-ambiguous.md`.
- Probe of .NET `Uri`: decimal (`2130706433`), hex (`0x7f000001`), octal (`0177.0.0.1`) and short (`127.1`) IPv4
  forms are all normalised to `127.0.0.1` with `HostNameType = IPv4`; `user@host` puts `user` in `UserInfo`;
  `[::ffff:192.168.0.1]` is an IPv4-mapped IPv6 address.

## New rules (in this order, after the existing ones)
| Rule | Rejects | Message |
|---|---|---|
| No credentials | `https://paypal.com@evil.example/`, `https://user:pass@host/` | "URLs with a user name or password are not allowed." |
| No private or local IP addresses | loopback, private (10/8, 172.16/12, 192.168/16), link-local (169.254/16, fe80::/10), CGNAT (100.64/10), unspecified (0.0.0.0, ::), multicast, broadcast, IPv6 unique-local (fc00::/7), in **any notation**, including IPv4 mapped into IPv6 | "Links to private or local network addresses are not allowed." |
| No internal host names | `localhost`, `*.localhost`, `*.local`, `*.internal`, `*.lan`, `*.home.arpa`, and single-label names (`http://intranet/`) | "Links must point to a public host name." |
| No links to this service | the configured public host (`ShortLinks:PublicBaseUrl`) | "Links to this service's own short links are not allowed." |
| Optional denylist | domains in `Abuse:BlockedDomains` and their subdomains | "Links to this domain are not allowed." |

## Constraints
- Rules live in Core (`TargetUrlValidator` + a `TargetUrlPolicy` with own hosts and blocked domains); no DNS lookups
  (documented in ADR-0004: we never fetch targets).
- Public IP addresses stay allowed (`http://93.184.216.34/`).
- Configuration only, no secrets. The denylist is empty by default; examples in docs stay generic.

## Acceptance criteria
- [ ] A table-driven test matrix of about 30 cases (accepted and rejected), each rejected case asserting its reason.
- [ ] Every IP notation of 127.0.0.1 and the IPv4-mapped private address is rejected.
- [ ] The API returns 400 with the reason for a blocked URL; existing behaviour for valid URLs is unchanged.
- [ ] `scripts/verify.ps1` passes.

## Out of scope
- Reputation services, DNS resolution, checking targets already stored (validation applies at creation).

## Risks
- **Rejecting legitimate URLs** (for example an intranet link a company wants to share). Mitigation: clear message;
  rules documented; deliberately conservative toward visitors' safety.
- **A public name resolving to a private IP** (`10.0.0.1.nip.io`) passes. Documented, accepted.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 0 | Before the spec: probe how .NET `Uri` parses tricky hosts | Decimal/hex/octal/short IPv4 all normalise to `127.0.0.1`; `user@host` → `UserInfo`; IPv4-mapped IPv6 kept as IPv6 | Shaped the design: check the *parsed* host, no custom IP parser; unwrap IPv4-mapped addresses. |
| 1 | Initial spec above | `NetworkAddress.IsPrivateOrLocal`, `TargetUrlPolicy` (own hosts + denylist, IDN-normalised), new rules in `TargetUrlValidator`, policy from configuration, 38-case unit matrix, 5 API tests | Removed a pointless `ToUpperInvariant()` before `IdnHost` (same slip as in BF-06). All cases passed first time. |
| 2 | Does the matrix actually bite? | Disabled the `192.168.0.0/16` rule on purpose | Exactly the two dependent cases failed (plain and IPv4-in-IPv6); rule restored. Pending scenario review. |

## Outcome
- **Decision:**
- **AI-LOG row:**
- **Commit(s):**
- **Sign-off:** scenario review
