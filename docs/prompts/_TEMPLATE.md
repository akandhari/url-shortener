<!--
How to use this template
- Copy to docs/prompts/<TASK-ID>-<short-name>.md (e.g. BF-06-async-click-recorder.md).
- Fill in everything above "Iterations" BEFORE asking the AI for code. This file is the prompt.
- Keep it short: if a section has nothing to say, write "None" rather than deleting it.
- Fill in "Iterations" and "Outcome" as the work happens, not afterwards.
- Commit this file with (or before) the code it produced.
-->

# <TASK-ID>: <short title>

| | |
|---|---|
| **Scenario** | Greenfield / Brownfield (CR-xxx) / Ambiguous |
| **Depends on** | <TASK-IDs, or None> |
| **Requirements** | <FR-x, NFR-x from docs/REQUIREMENTS.md> |
| **Needs my sign-off?** | No / Yes: schema · security · API contract · dependency · CI |
| **Status** | Draft → Ready → In progress → Done |

## Goal
<!-- 1–2 lines: what is true when this task is done, and why it matters. -->

## Context
<!-- What the AI needs to know. Files to read, current behaviour, related ADRs, decisions already made. -->
- Files:
- Current behaviour:
- Related decisions:

## Constraints
<!-- Hard rules for this task, on top of CLAUDE.md. -->
- No new packages unless listed here:
-

## Acceptance criteria
<!-- Each one must be checkable: by a test, a command or a visible result. -->
- [ ]
- [ ] `scripts/verify.ps1` passes (build, format, tests)

## Test plan
<!-- Which tests prove the criteria above. Say which must fail before the change, if any. -->
- Unit:
- Integration:

## Out of scope
-

## Risks
<!-- What could go wrong with this change, and how we'd notice. -->
-

---

## Iterations
<!-- One row per round with the AI. Record pushback and why; that is the evidence of judgment. -->

| # | What I asked / changed in the prompt | What the AI returned | My response and why |
|---|---|---|---|
| 1 | Initial spec above | | |

## Outcome
- **Decision:** Kept / Changed / Rejected
- **AI-LOG row:** #
- **Commit(s):**
- **Sign-off:** N/A / SIGNOFF #
