# BugCam Codex Agent Orchestration Design

Date: 2026-07-31
Status: Awaiting written-spec review

## Goal

Configure project-scoped Codex agents so BugCam development continues block by block with visible delegation, one code writer at a time, independent review, and Unity-specific API verification.

## Chosen approach

Use Codex project configuration and three narrow custom agents. The primary chat remains the coordinator and is not duplicated as a custom agent.

Rejected alternatives:

- Multiple parallel implementation agents: too much conflict risk in Unity source, `.meta` files, scenes, and shared tests.
- A new BugCam skill now: the development loop has not repeated enough to justify another workflow layer.

## Components

### Primary coordinator

The primary Codex task owns `docs/PLAN.md`, scope decisions, dispatch order, final verification evidence, `docs/STATUS.md`, and the single block commit. It never dispatches two code-writing agents at the same time.

### `bugcam_implementer`

A write-capable implementation agent for one explicitly bounded task inside the active PLAN block. It must use tests first for fixes and behavior, preserve the fixed architecture, avoid unrelated refactors, run the narrowest available verification, and return changed files, commands, results, and concerns. It does not commit unless the coordinator explicitly requests it.

### `bugcam_reviewer`

A read-only reviewer. It checks the supplied diff against `AGENTS.md`, the active PLAN block, SPEC precedence, test coverage, deterministic-core constraints, allocation risks, and Unity lifecycle cleanup. It reports findings by severity and does not edit files.

### `unity_researcher`

A read-only specialist for version-sensitive Unity APIs and local-editor facts. It checks the installed Unity version and local package/editor sources first, then current official Unity documentation when needed. It returns exact API names, version scope, and links; it never changes project files.

## Configuration

Create these project-scoped files after approval:

- `.codex/config.toml`
- `.codex/agents/bugcam-implementer.toml`
- `.codex/agents/bugcam-reviewer.toml`
- `.codex/agents/unity-researcher.toml`

The project config enables agents and limits spawned threads to three. Agent model names and reasoning effort remain unset so they inherit the current primary-task configuration rather than becoming stale.

`bugcam_reviewer` and `unity_researcher` use `sandbox_mode = "read-only"`. The implementer inherits the parent workspace permissions and is restricted by its developer instructions and `AGENTS.md`.

## Workflow

1. The coordinator selects the next incomplete task from the active PLAN block and records the expected verification.
2. If an API is version-sensitive, `unity_researcher` resolves it first. Independent research or log analysis may run in parallel.
3. Exactly one `bugcam_implementer` edits the project and runs focused tests.
4. `bugcam_reviewer` reviews the resulting diff read-only. Important findings return to the same implementer for one fix round.
5. The coordinator runs final block verification, updates `STATUS.md`, and creates the one allowed block commit.

Day 2 work is never dispatched before the Day 1 hard gate. Landing-page or evidence-asset work may later use separate worktrees only after those tasks become independent and the core checkpoint passes.

## Failure handling

- Missing context: the coordinator supplies only the relevant PLAN/SPEC section and affected interfaces.
- Unity API uncertainty: pause implementation and dispatch `unity_researcher`; do not guess.
- Failed test or unexpected behavior: reproduce first, collect logs, then use systematic debugging before changing code.
- Review failure: return concrete findings to the implementer; do not let the coordinator silently patch around review.
- Two unsuccessful fix rounds on the same issue: stop that task and report the blocker with test output and attempted fixes.

## Verification

Configuration is complete when:

- all TOML files parse;
- Codex discovers all three custom agents from the repository;
- reviewer and researcher are read-only;
- `AGENTS.md` contains the dispatch and single-writer rules;
- a dry-run delegation can ask `unity_researcher` for the installed Unity version without changing the worktree;
- `git diff` shows no changes outside the orchestration files and the intentional `AGENTS.md` addition.

