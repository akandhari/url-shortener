# GF-05: Web page to shorten a URL

| | |
|---|---|
| **Scenario** | Greenfield |
| **Depends on** | GF-04 |
| **Requirements** | FR-4, NFR-3 |
| **Needs my sign-off?** | Yes: security setting (Content-Security-Policy for the page) |
| **Status** | Done |

## Goal
A simple page at `/` where anyone can paste a long URL, get the short link, copy it, and see its click count,
without needing API tools.

## Context
- The API from GF-04: `POST /api/links`, `GET /api/links/{code}`, `GET /{code}`; errors are ProblemDetails with a readable `detail`.
- `/{code}` is a catch-all with one path segment, so a file like `/app.js` would also match it.

## Constraints
- Plain HTML, CSS and JavaScript in `src/UrlShortener.Api/wwwroot/`. No framework, no build step, no CDN.
- Script and styles in **separate files** (`app.js`, `app.css`), no inline script, so the page can use a strict
  Content-Security-Policy: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:;
  connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'`.
- The CSP is applied **only to the static page files**. `/scalar` loads its script from a CDN and would break under it.
  Site-wide security headers come in AB-05.
- Static files must be served **before** route matching. Otherwise `/app.js` is claimed by the `/{code}` endpoint and
  returns 404 (the static file middleware skips requests that already matched an endpoint).
- API data is shown with `textContent`, never `innerHTML`, so a malicious target URL can't inject HTML/script (XSS).
- Readable on a phone (a single column, a responsive width).

## Page behaviour
1. Input "Long URL" + **Shorten** button (Enter also submits).
2. On success: show the short URL as a link, a **Copy** button, the target URL, and "Clicks: N" with a **Refresh** button.
3. On error: show the API's `detail` message (e.g. "Only http and https URLs are allowed.") in a visible error area.
4. The button is disabled while the request is in flight (no double submits).

## Acceptance criteria
- [ ] `GET /` returns the page (200, `text/html`).
- [ ] The page's responses carry the CSP header; `/scalar` does not (and still works).
- [ ] `GET /app.js` and `GET /app.css` return 200 with the right content types, not the 404 from `/{code}`.
- [ ] Manual check in a browser: shorten → copy → open the short link → Refresh shows Clicks: 1; an invalid URL shows the error text.
- [ ] No inline `<script>` or `style=` in `index.html`; no `innerHTML` in `app.js`.
- [ ] `scripts/verify.ps1` passes.

## Test plan
- Integration: `StaticPageTests`: `/` HTML + CSP header, `/app.js` and `/app.css` served, `/scalar` without CSP.
- Manual browser check (JavaScript behaviour isn't unit tested; noted as a limitation).

## Out of scope
- Stats panel (BF-07), alias and expiry fields (AB), site-wide security headers (AB-05).

## Risks
- **Middleware order is easy to break later.** Mitigation: the `/app.js` test fails if it is.
- **No automated JavaScript tests.** Mitigation: kept tiny; manual check recorded; noted as a limitation.

---

## Iterations

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | `wwwroot/index.html`, `app.css`, `app.js` (textContent only, ProblemDetails `detail` shown on error, button disabled in flight), `ContentSecurityPolicy` with one commented line per directive, 6 integration tests | The first draft of the "no inline handlers" test was convoluted and hard to read; **rejected** and rewritten as one regex assertion before running it. |
| 2 | Prove the routing gotcha before fixing it | Wired static files the naive way (no explicit `UseRouting`): `/app.js` and `/app.css` returned 404 from the `/{code}` endpoint, 4 of 6 tests red | Confirms the risk is real, not theoretical. |
| 3 | Fix | `UseDefaultFiles` + `UseStaticFiles` (CSP via `OnPrepareResponse`) **before** an explicit `UseRouting` | All 79 tests green. |
| 4 | Browser check | Headless-browser tool unavailable on this machine (missing driver). Checked over HTTP instead: CSP on `/`, assets served, no CSP on `/scalar`, short codes still redirect | Checked by me in a real browser: shorten, copy, redirect, Refresh → Clicks 1, error message, CSP header. **Approved and signed off** (SIGNOFF #5). |

## Outcome
- **Decision:** Changed (test rewritten for readability)
- **AI-LOG row:** #9
- **Commit(s):** GF-05 commit
- **Sign-off:** SIGNOFF #5
