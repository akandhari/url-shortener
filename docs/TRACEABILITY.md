# Traceability: requirement → task → test

Every requirement in [REQUIREMENTS.md](REQUIREMENTS.md), the tasks that delivered it (specs in [prompts/](prompts/)),
and the tests that prove it.

| Req | Requirement | Tasks | Verified by |
|---|---|---|---|
| FR-1 | Create a short link from an http/https URL | GF-02, GF-03, GF-04 | `LinkServiceTests.CreateAsync_*`, `EfLinkRepositoryTests`, `LinkEndpointsTests.Create_returns_201_with_location_and_the_link` |
| FR-2 | Redirect; unknown → 404 | GF-02, GF-04, BF-02, BF-08 | `RedirectCharacterizationTests`, `LinkEndpointsTests.Redirect_*`, `Unknown_codes_return_404_problem_details`, `CachedRedirectLookupTests` |
| FR-3 | Link details | GF-04 | `LinkEndpointsTests.Details_return_the_same_shape_as_create` |
| FR-4 | Web page | GF-05, BF-07, AB-03b, AB-04 | `StaticPageTests`; manual browser check (recorded in GF-05 spec) |
| FR-5 | Clicks per day, top referrers | BF-05, BF-06, BF-07 | `LinkStatsTests.Stats_return_exact_clicks_per_utc_day_and_top_referrers`, `ClickRecordingTests` |
| FR-6 | Reject unsafe or invalid URLs | GF-02, AB-02 | `TargetUrlValidatorTests`, `TargetUrlAbuseTests` (38-case matrix), `BlockedTargetApiTests` |
| FR-7 | Rate limit per client | AB-03 | `RateLimitTests` |
| FR-8 | Custom alias with rules | AB-03b | `AliasRulesTests`, `AliasApiTests` |
| FR-9 | Expiry and disable → 410 | AB-04 | `LinkServiceTests.Expired_link_*`, `DisableAsync_*`, `ExpiryAndDisableTests` |
| NFR-1 | Redirect fast: no synchronous write | BF-06, BF-08, WR-02 | `ClickRecordingTests`, `CachedRedirectLookupTests`, load test: p95 12.3 ms at ~6,400 redirects/s ([TESTING.md](TESTING.md)) |
| NFR-2 | Reliability: unique codes, health, correct under concurrency, flush on shutdown | GF-03, BF-03, BF-06, BF-09 | `ConcurrentClickTests` (50/50), `EfLinkRepositoryTests.Taken_code_*`, `HealthEndpointTests`, `ClickRecordingTests.Stopping_the_writer_flushes_queued_clicks`, `UpgradeFromV01Tests` |
| NFR-3 | Security | GF-04, GF-05, AB-02…AB-05 | `TargetUrlAbuseTests`, `RateLimitTests`, `AliasApiTests`, `ExpiryAndDisableTests`, `SecurityHeadersTests`, `StaticPageTests`, `LinkEndpointsTests.Short_url_uses_the_configured_base_url_not_the_host_header`; gitleaks in CI |
| NFR-4 | Privacy: no IPs, referrer host only | BF-06 | `ClickEventTests`, `ClickRecordingTests.Each_redirect_stores_a_click_event_with_the_referrer_host_only` |
| NFR-5 | Maintainability: layers, zero warnings, tests, CI | GF-01 and every task | `verify.ps1` gates on every commit; 93.5% line coverage |
| NFR-6 | Runs with `dotnet run` or `docker run`, no external services | GF-01, GF-03, WR-01 | README steps; Docker smoke test in [TESTING.md](TESTING.md) |
