# Sign-off register

High-impact changes I approved personally (see rule 4 in `AI-USAGE-POLICY.md`).

| # | Date | Change | Category | Risk | Rollback | Commit | Approved by |
|---|------|--------|----------|------|----------|--------|-------------|
| 1 | 2026-09-30 | Quality gates (`verify.ps1`), CI workflow with secret scan, 5 test-only packages | CI · dependencies | Gates too loose would let defects through; packages are test-only and the vulnerability scan is clean | Revert the GF-01 commit | GF-01 commit | Aakash Kandhari |
