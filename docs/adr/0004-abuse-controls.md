# ADR-0004: Abuse controls built into the app

- **Status:** Accepted (AB-01)
- **Date:** 2026-10-01

## Context
"Make it safe from abuse" can mean many things (see the threat model in
[03-ambiguous.md](../scenarios/03-ambiguous.md)). The prototype must run with no external services (NFR-6) and has no
user accounts.

## Decision
Build the controls the app can enforce on its own, visitors first:
1. **Validate targets strictly** at creation: http/https only, no credentials, no private/loopback/link-local IPs in
   any notation, no internal host names, no links to our own host, optional domain denylist.
2. **Rate-limit per client IP** with ASP.NET Core's built-in rate limiter: strict for creating links, looser for
   redirects and lookups. Limits live in configuration.
3. **Custom aliases only with rules:** format, reserved words, impersonation blocklist.
4. **Expiry and disable** return **410 Gone**. Disabling requires an **admin key** from configuration (never in the
   repo); without a configured key the endpoint is off.
5. **Security headers** on every response.

## Options considered
| Option | Why not now |
|---|---|
| External URL reputation (Safe Browsing / Web Risk) | Needs an external service and key; first thing to add for production |
| CAPTCHA | Hurts usability; rate limits first |
| User accounts | Product-wide change; listed as the top follow-up |
| Rate limiting at the proxy/CDN instead of the app | Better at scale, but not available in a laptop prototype; app limits stay as defence in depth |

## Consequences
- ✅ The most likely abuses (spam, deceptive or internal targets, enumeration, impersonation) are blocked or slowed
  without any external dependency.
- ⚠️ Rate limits are **per instance** (in-memory). Several instances need a distributed limiter (e.g. Redis) or the proxy.
- ⚠️ Behind a proxy, the client IP must come from trusted forwarded headers, or every visitor shares one limit.
- ⚠️ Host-name checks don't resolve DNS: a public name pointing to a private IP is not caught (acceptable because we
  never fetch targets).
- ⚠️ The admin key is a shared secret, a stopgap until real authentication.
