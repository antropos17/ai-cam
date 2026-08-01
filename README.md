# AI Cam (BugCam)

Repository / workspace name: **AI Cam** (`ai-cam`).  
Product implemented in this codebase: **BugCam** — chaos testing and repeatability diagnostics for Unity scenes.

One sentence: perturb initial state → rerun → measure divergence → prove it with recorded evidence.

> This is **not** a computer-vision camera app. There are no face detectors, cloud vision APIs, or ML model weights in the current tree. The word “AI” is part of the workspace/repo name only; v0.1 product scope explicitly excludes AI features.

## What works today

Based on the code under `Assets/BugCam/` (early Block 1.x):

- Unity 6.3 URP project with a BugCam package layout (`Core`, `Editor`, `Tests`)
- `SimulationHarness` — Script simulation mode, local `PhysicsScene`, fixed step `0.02s`, per-run scene recreation
- Determinism probe helpers and physics settings probe (Editor)
- Procedural tower demo scene generator + `TowerScene.unity`
- EditMode contract tests for the Core simulation API (reflection-based)
- Editor automation hooks to run EditMode tests / export a tower preview on request files under `Library/`

Not implemented yet (planned in `docs/PLAN.md` / `docs/SPEC.md`): DivergenceEngine, adaptive epsilon search, ghost trajectories, retro cameras, MP4/evidence export, UTF flaky-test attribute, browser viewer, CI gate, attribution.

## Architecture

```
Assets/BugCam/
├── Core/       SimulationHarness, DeterminismProbe, BugCamConstants
├── Evidence/   (planned — GhostRenderer, RetroPlayer, EvidenceCameras, Exporter)
├── Editor/     Probes, tower scene generator, test automation
└── Tests/      TowerScene.unity, EditMode contract tests
```

Rules from project docs:

- `Core` must not depend on `Evidence` or `Editor`
- State stride (planned): 14 floats per body per step — `pos, rot, vel, angVel, sleeping`
- Lengths inside Core are metres; millimetres are display-only

## Stack

| Layer | Choice |
|---|---|
| Engine | Unity **6000.3.21f1** (target: Unity 6.3 LTS, `6000.3.20f1`+) |
| Language | C# |
| Render pipeline | Universal Render Pipeline (URP) 17.3 |
| Physics | Built-in 3D Physics (`Rigidbody` / `Collider`, local `PhysicsScene`) |
| Tests | Unity Test Framework 1.6 (EditMode) |
| Editor tooling (optional) | [Unity MCP](https://github.com/CoplayDev/unity-mcp) via Package Manager git URL |

No Docker, no Node/Python app server, no database.

## Requirements

- Windows (current development host)
- Unity Hub + Unity Editor **6.3 LTS** matching or newer within 6.3 (project opened as `6000.3.21f1`)
- Git (Package Manager fetches the Unity MCP package over HTTPS)
- Optional: GitHub CLI if you manage remotes with `gh`

## Install

1. Clone this repository.
2. Open the folder in Unity Hub → **Add** → select the project root (the directory that contains `Assets/`, `Packages/`, `ProjectSettings/`).
3. Let Unity restore packages (`Packages/manifest.json` / `packages-lock.json`).
4. Open `Assets/BugCam/Tests/TowerScene.unity` or use the Editor menus under BugCam to regenerate the tower scene.

There is no `npm install` / `pip install` step for the product itself.

## Environment variables

No runtime secrets or `.env` files are required by the current code.

If you add cloud features later, keep secrets out of git:

1. Copy `.env.example` (create one when variables appear) to `.env`
2. Ensure `.env` stays gitignored

Unity MCP / Claude / Codex agent configs in this repo do not embed API keys.

## Run

1. Open the project in the Unity Editor.
2. Set Physics **Simulation Mode** as required by the harness (Script) when running probes — see `docs/PLAN.md` Block 1.1.
3. Use BugCam Editor menu items / probes described in `docs/STATUS.md` and `docs/PLAN.md`.
4. Local `PhysicsScene` creation via `SceneManager.CreateScene(..., LocalPhysicsMode.Physics3D)` requires Play Mode. Simulation correctness tests live in the PlayMode assembly.

Headless / batchmode:

```text
"<UnityEditor>\Unity.exe" -batchmode -nographics -projectPath "<repo>" -runTests -testPlatform EditMode -testResults TestResults/EditMode.xml -logFile - -quit
"<UnityEditor>\Unity.exe" -batchmode -nographics -projectPath "<repo>" -runTests -testPlatform PlayMode -testResults TestResults/PlayMode.xml -logFile - -quit
```

Editor automation: write `Library/BugCamTest.request` with `all`, `editmode`, or `playmode`. Results land at `Library/BugCamTestResults.EditMode.xml`, `Library/BugCamTestResults.PlayMode.xml`, and `Library/BugCamTestResults.xml`.

Do not commit generated `Library/`, `Logs/`, or `TestResults/`.

## Tests

- EditMode assembly: `BugCam.Tests` — reflection/contract and Editor generator tests
- PlayMode assembly: `BugCam.Tests.PlayMode` — `SimulationHarness` / local `PhysicsScene` runs

There is no separate lint/typecheck toolchain outside the Unity compiler for this project.

## Folder structure

```text
.
├── Assets/BugCam/          Product code, demo scene, tests
├── Packages/               manifest + lock (URP, Test Framework, Unity MCP)
├── ProjectSettings/        Unity project settings
├── docs/                   SPEC, PLAN, STATUS, design notes
├── AGENTS.md / CLAUDE.md   Agent operating rules for this repo
├── .gitignore
└── README.md
```

Ignored locally (do not publish): `Library/`, `Temp/`, `Logs/`, `UserSettings/`, exports, `*.bugcam`, videos, `.env`, credentials.

## Current limitations

- Day 1 checkpoint **not passed** (`docs/STATUS.md`)
- Divergence search, ghosts, evidence cameras, and export pipeline are not shipped
- Repeatability claims are **environment-scoped** (Unity version, physics settings, threading mode) and meaningless without a recorded snapshot
- Sensitivity is not automatically a bug — fragile scenes can be intentional design
- No absolute cross-platform determinism guarantee
- v0.1 excludes DOTS Physics, 2D, Cloth, CI integration, browser viewer, cloud, and AI features

## Development status

Early implementation / documentation-driven build. Active plan block is tracked in `docs/STATUS.md`. Source of truth:

1. `docs/SPEC.md` — full product vision  
2. `docs/PLAN.md` — what is built now (wins over SPEC for v0.1)  
3. `docs/STATUS.md` — session memory and open blockers  

## Security and privacy

- BugCam records **physics state** (positions, rotations, velocities, sleep flags) from scenes you choose to run. It does not capture webcams, microphones, or biometric face data.
- Do not commit exported replays, evidence videos, or `.bugcam` capsules that contain proprietary game content you are not allowed to share.
- Keep API tokens, Unity Cloud credentials, and `.env` files out of the repository.
- Agent telemetry files named `events.jsonl` are gitignored because they can contain prompt text from local tooling.
- The optional Unity MCP package is editor tooling; treat any MCP bridge as a local developer surface, not a production endpoint.

## License

No license file is included yet. Rights are reserved by the author until a license is chosen.
