# Sign-off register

High-impact changes I approved personally (see rule 4 in `AI-USAGE-POLICY.md`).

| # | Date | Change | Category | Risk | Rollback | Commit | Approved by |
|---|------|--------|----------|------|----------|--------|-------------|
| 1 | 2026-09-30 | Quality gates (`verify.ps1`), CI workflow with secret scan, 5 test-only packages | CI · dependencies | Gates too loose would let defects through; packages are test-only and the vulnerability scan is clean | Revert the GF-01 commit | GF-01 commit | Aakash Kandhari |
| 2 | 2026-09-30 | Default branch renamed `master` → `main`; CI runs on pushes to `main` and `feature/**` and on PRs to `main` | CI | More CI runs (free for public repos); branch rename breaks old links to `master` (none shared yet) | Revert the commit, rename back | infra commit | Aakash Kandhari |
| 3 | 2026-09-30 | First database schema (`InitialCreate`: `Links` table, unique index on `Code`); packages `Microsoft.EntityFrameworkCore.Sqlite` and `.Design` 10.0.12; local tool `dotnet-ef` 10.0.12 | schema · dependencies | Migrate-on-startup would race with several instances (single node here); SQLite single writer | Revert the commit; delete `urlshortener.db` | GF-03 commit | Aakash Kandhari |
| 4 | 2026-09-30 | Public API contract: `POST /api/links` (201/400/503), `GET /api/links/{code}` (200/404), `GET /{code}` (302/404), ProblemDetails errors; packages `Microsoft.AspNetCore.OpenApi` 10.0.12 and `Scalar.AspNetCore` 2.17.12 | API contract · dependencies | Contract changes later would break clients; OpenAPI UI is public in this prototype | Revert the commit | GF-04 commit | Aakash Kandhari |
| 5 | 2026-09-30 | Content-Security-Policy on the web page: `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'` (not applied to `/scalar`) | security | Too strict would silently break page features; too loose gives false confidence. Scalar has no CSP until AB-05 | Revert the commit | GF-05 commit | Aakash Kandhari |
