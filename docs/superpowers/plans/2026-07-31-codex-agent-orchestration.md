# Codex Agent Orchestration Implementation Plan

> **For agentic workers:** Execute this plan as one bounded setup task, then run an independent read-only review. Do not create a second writer because the Unity worktree already contains uncommitted Block 1.1 files.

**Goal:** Add durable project-scoped Codex roles and single-writer delegation rules for BugCam.

**Architecture:** The primary task remains the coordinator. Three project agents live under `.codex/agents`: one bounded implementer and two read-only specialists. `.codex/config.toml` explicitly enables the `multi_agent` feature and agents, then caps spawned threads; `AGENTS.md` defines the dispatch sequence that every future task inherits.

**Tech Stack:** Codex project TOML configuration, Markdown repository instructions, Python 3.10.6 with installed `tomli` validation, Git.

## Global Constraints

- Preserve all pre-existing modified and untracked Unity project files.
- Never run two code-writing agents at the same time.
- Do not hardcode model names or reasoning effort.
- `bugcam_reviewer` and `unity_researcher` must use `sandbox_mode = "read-only"`.
- The primary coordinator owns final verification, `docs/STATUS.md`, and the one commit for each product block.
- This setup is infrastructure and must not change Unity runtime code.

---

### Task 1: Configure project-scoped agents

**Files:**

- Create: `.codex/config.toml`
- Create: `.codex/agents/bugcam-implementer.toml`
- Create: `.codex/agents/bugcam-reviewer.toml`
- Create: `.codex/agents/unity-researcher.toml`
- Modify: `AGENTS.md`

**Interfaces:**

- Consumes: Codex project configuration discovery from the repository root.
- Produces: custom agent names `bugcam_implementer`, `bugcam_reviewer`, and `unity_researcher`.

- [ ] **Step 1: Record the clean configuration baseline**

Run:

```powershell
Test-Path .codex/config.toml
Get-ChildItem .codex/agents -ErrorAction SilentlyContinue
```

Expected before implementation: `.codex/config.toml` is absent and no project agent files are listed.

- [ ] **Step 2: Create `.codex/config.toml`**

```toml
[features]
multi_agent = true

[agents]
enabled = true
max_concurrent_threads_per_session = 3
interrupt_message = true
```

- [ ] **Step 3: Create the bounded implementation agent**

Create `.codex/agents/bugcam-implementer.toml`:

```toml
name = "bugcam_implementer"
description = "Single-writer Unity implementation agent for one bounded task in the active BugCam PLAN block."
developer_instructions = """
Work on exactly one task supplied by the primary coordinator.
Read AGENTS.md and the referenced task brief before editing.
Use tests first for bug fixes and behavior changes, preserve the fixed BugCam architecture, and avoid unrelated refactors.
Never start Day 2 work before the Day 1 hard gate.
Run the narrowest relevant verification and report changed files, commands, results, and concerns.
Do not commit unless the primary coordinator explicitly requests it.
"""
```

- [ ] **Step 4: Create the read-only review agent**

Create `.codex/agents/bugcam-reviewer.toml`:

```toml
name = "bugcam_reviewer"
description = "Read-only BugCam reviewer for spec compliance, correctness, determinism risks, and missing tests."
sandbox_mode = "read-only"
developer_instructions = """
Review only the task brief, supplied diff, test evidence, AGENTS.md, and the active PLAN block.
Prioritize correctness, deterministic-core constraints, allocations inside simulation loops, Unity object and scene cleanup, and missing tests.
Report findings by severity with exact file and line references.
Give separate verdicts for specification compliance and code quality.
Do not edit files, run destructive commands, or broaden the task scope.
"""
```

- [ ] **Step 5: Create the read-only Unity research agent**

Create `.codex/agents/unity-researcher.toml`:

```toml
name = "unity_researcher"
description = "Read-only specialist for installed-editor facts and version-sensitive Unity APIs used by BugCam."
sandbox_mode = "read-only"
developer_instructions = """
Resolve one concrete Unity API or environment question at a time.
Check ProjectSettings, Packages, the installed editor, and local package or reference sources before using current official Unity documentation.
State the installed Unity version, exact API or setting name, version scope, evidence path or link, and any uncertainty.
Never guess an API name and never modify project files.
Return a concise research note for the primary coordinator.
"""
```

- [ ] **Step 6: Add durable dispatch rules to `AGENTS.md`**

Append this section:

```markdown
## Agent orchestration
- The primary task is the coordinator: it selects the next incomplete PLAN task, supplies a bounded brief, owns final verification, updates `docs/STATUS.md`, and creates the block commit.
- Use `unity_researcher` for version-sensitive Unity APIs or installed-editor facts before implementation; it is read-only.
- Use one `bugcam_implementer` at a time for a bounded write task. Never dispatch parallel writers against this Unity worktree.
- After each implementation diff, use `bugcam_reviewer` read-only. Important findings return to the same implementer before the task advances.
- Research and log analysis may run in parallel only when they do not edit project files or block the coordinator's immediate next action.
- Preserve pre-existing user changes. An agent may touch only the files named in its brief.
```

- [ ] **Step 7: Enable the Codex 0.103 compatibility flag once, verify activation, and assert all TOML contracts**

Codex CLI 0.103.0 loads feature configuration for `codex features` from `~/.codex/config.toml`, so the project-scoped flag alone leaves the effective `multi_agent` state false on that client. Keep `[features] multi_agent = true` in the project config for newer clients, and use the supported one-time global enable command for 0.103.0.

Run:

```powershell
codex --version
codex features --help
codex features enable multi_agent
codex features list
python -c "import pathlib,tomli as tomllib; root=pathlib.Path('.codex'); cfg=tomllib.loads((root/'config.toml').read_text('utf-8')); assert cfg['features']['multi_agent'] is True; assert cfg['agents']['enabled'] is True; assert cfg['agents']['max_concurrent_threads_per_session']==3; assert cfg['agents']['interrupt_message'] is True; files=list((root/'agents').glob('*.toml')); assert len(files)==3; data=[tomllib.loads(p.read_text('utf-8')) for p in files]; assert {d['name'] for d in data}=={'bugcam_implementer','bugcam_reviewer','unity_researcher'}; assert all(isinstance(d.get(k),str) and d[k].strip() for d in data for k in ('name','description','developer_instructions')); assert all('model' not in d and 'model_reasoning_effort' not in d for d in data); by_name={d['name']:d for d in data}; assert by_name['bugcam_reviewer'].get('sandbox_mode')=='read-only'; assert by_name['unity_researcher'].get('sandbox_mode')=='read-only'; print('Codex agent config OK')"
```

Expected: `codex --version` prints `codex-cli 0.103.0`; the help text says configuration is loaded from `~/.codex/config.toml`; after the one-time enable command, the `multi_agent` row ends in `experimental       true`; and the assertion prints `Codex agent config OK`.

- [ ] **Step 8: Verify scope and whitespace**

Run:

```powershell
python -c "import pathlib; root=pathlib.Path('.'); paths=sorted((root/'.codex').rglob('*.toml'))+[root/'AGENTS.md',root/'docs/superpowers/plans/2026-07-31-codex-agent-orchestration.md']; missing=[str(p) for p in paths if not p.is_file()]; assert not missing, 'Missing files: '+', '.join(missing); bad=[f'{p}:{line_no}' for p in paths for line_no,line in enumerate(p.read_text('utf-8').splitlines(),1) if line.endswith((' ','\t'))]; assert not bad, 'Trailing whitespace: '+', '.join(bad); print(f'Whitespace OK: {len(paths)} files')"
git status --short
```

Expected: `Whitespace OK: 6 files`; orchestration changes are limited to `.codex/**`, the intentional `AGENTS.md` addition, and this plan. Existing unrelated modified and untracked files remain untouched.

- [ ] **Step 9: Run independent review, then commit only orchestration files**

The reviewer must return both `Spec: PASS` and `Quality: PASS` before commit.

```powershell
git add -- .codex AGENTS.md docs/superpowers/plans/2026-07-31-codex-agent-orchestration.md docs/superpowers/specs/2026-07-31-codex-agent-orchestration-design.md
git commit -m "chore: configure BugCam Codex agents" -- .codex AGENTS.md docs/superpowers/plans/2026-07-31-codex-agent-orchestration.md docs/superpowers/specs/2026-07-31-codex-agent-orchestration-design.md
```

Expected: one infrastructure commit; existing Unity and product-document changes remain uncommitted.
