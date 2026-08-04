---
description: Check a draft agent prompt against the prompt rules and return a corrected version plus violations
---

# Promptcheck

$ARGUMENTS is a draft prompt for a Claude Code agent. Check it against the rules below and return two things: the corrected version of the prompt, and the list of violations you found in the draft.

## Rules

- The prompt must be in English.
- The prompt must be a single solid block with no blank lines.
- The prompt must state intent and context before the task.
- The prompt must not ask the model to reproduce its internal reasoning — that triggers reasoning_extraction on Fable 5.
- A review prompt must not say "be conservative" or "only high-severity".
- Verification rules are model-specific and opposite:
  - For Fable 5 long runs, fresh-context verifier subagents and interval self-checks against the spec are good — keep them, and add them where they are missing.
  - For Opus 5, routine double-check instructions, final verification steps and verifier subagents must be removed.
- Scope must be explicitly bounded.
- The report format must lead with the outcome.
