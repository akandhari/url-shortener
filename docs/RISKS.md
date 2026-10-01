# Risk register

Likelihood and impact are my estimates for running this prototype as-is with real users (H / M / L).

| # | Risk | L | I | Mitigation in place | Residual risk / next step |
|---|---|---|---|---|---|
| R1 | **Phishing or malware links** use our domain's reputation | H | H | Scheme rules, no credentials in URLs, no private targets, denylist, rate limits, expiry, admin disable (410) | No reputation check of destinations → add Safe Browsing / Web Risk at creation and periodically |
| R2 | **Redirecting visitors into private networks** (router pages, cloud metadata) | M | H | Private/loopback/link-local/CGNAT/multicast blocked in every IP notation; internal host names blocked | Public names that resolve to private IPs pass (we never fetch targets); revisit if previews are added |
| R3 | **Mass link creation / storage exhaustion** | M | M | 10 creates per minute per IP; URL ≤ 2048 characters | Per-IP limits can be spread across many IPs → accounts or CAPTCHA |
| R4 | **Code enumeration** (scraping links) | L | M | 2.2 trillion random Base58 codes; redirect and lookup rate limits | Stats are public to anyone with the code → ownership with accounts |
| R5 | **Lost or delayed click counts** | M | L | Append-only events, atomic SQL count, drain on graceful shutdown, drops counted and logged | Crash loses buffered clicks; durable queue for production (ADR-0003) |
| R6 | **Lost updates under concurrency** | – | – | Fixed in v0.2 (BF-03 red 1/50 → green 50/50) | None known |
| R7 | **Unauthorised take-downs** | M | H | Disable needs an admin key from configuration, constant-time compare, off when unset | Shared secret → real authentication and roles |
| R8 | **Secrets committed to the repo** | L | H | Admin key only via environment / user secrets; gitleaks scans full history in CI | Rotate the key if it ever leaks |
| R9 | **Vulnerable dependencies** | M | M | Few packages, all Microsoft except Scalar; vulnerability scan on every build | New advisories after release → keep CI running on a schedule |
| R10 | **XSS / clickjacking through our pages** | L | M | `textContent` only, strict CSP on the page, nosniff, frame DENY, COOP | `/scalar` has no CSP → restrict or disable API docs in production |
| R11 | **Rate limits wrong behind a proxy** (everyone shares the proxy IP) | M | M | Documented | Forwarded headers from trusted proxies only |
| R12 | **More than one instance** | M | M | Database-level correctness (unique index, atomic counts) holds across instances | Per-instance buffer, cache and limits → Redis + durable queue |
| R13 | **SQLite limits** (single writer, no enforced text length) | M | L | One background writer; lengths enforced in code | PostgreSQL for production (ADR-0001) |
| R14 | **Migration problems** | L | H | Additive migrations only; upgrade test from v0.1; "no pending model changes" test | Migrate on startup races with several instances → deploy step |
| R15 | **AI-introduced defects** | M | M | Spec before code, gates (warnings as errors, analyzers, tests), my review of every task, characterization and mutation checks | Defects caught before merge are recorded in the AI-LOG and scenario docs |
| R16 | **Bots inflate analytics** | H | L | None | Bot filtering (product decision) |
| R17 | **Unbounded growth of click data** | M | L | Index keeps queries fast | Retention policy (product/legal decision) |
