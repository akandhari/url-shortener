# How I used AI on this project

I built this with Claude Code as a pair-programming assistant. I decided what to build,
reviewed everything it produced, and I own the result. These are the rules I followed.

## Rules

1. **Spec before code.** For any non-trivial task I first wrote a short spec in `docs/prompts/`:
   goal, constraints, acceptance criteria. That spec is what I gave the AI. Small edits
   (renames, typos, one-liners) didn't get a spec.
2. **I read every diff.** Nothing was accepted unread. For each meaningful piece of AI output
   I recorded in `docs/AI-LOG.md` whether I kept it, changed it or rejected it, and why.
3. **Gates before acceptance.** Build (warnings as errors), formatting and tests had to pass
   locally and in CI before I accepted a change. I reviewed AI-written tests like code and checked
   that each one would actually fail if the code were wrong.
4. **I personally approve high-impact changes:** database schema and migrations, security controls
   (URL validation, rate limits, headers), public API contract, new dependencies, CI changes.
   Those commits carry my `Signed-off-by` and a row in `docs/SIGNOFF.md`.
5. **Nothing sensitive goes into prompts.** No secrets, credentials, employer or client data, and
   not the original assignment document. Prompts contain only this repo's code and my own descriptions.
6. **New packages need a real reason** and must pass the vulnerability scan, even when the AI suggests them.
7. **Attribution in git.** Commits where AI wrote a meaningful part have `AI-Assisted: yes` and a
   `Co-Authored-By` trailer. Commits I wrote alone say `AI-Assisted: no`.

## What is (and isn't) in the repo
- In the repo: the task specs (`docs/prompts/`), the decision log (`docs/AI-LOG.md`),
  the sign-off register (`docs/SIGNOFF.md`) and the AI's working rules (`CLAUDE.md`).
- Not in the repo: raw chat transcripts. The specs and the log are the curated record of what was asked,
  what came back and what I decided.

## Audit trail from git
```
git log --format="%h %ad %s%n%(trailers)" --date=short
```
