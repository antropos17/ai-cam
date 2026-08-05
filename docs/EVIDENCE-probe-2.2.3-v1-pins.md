<!-- TRANSCRIPTION — provenance header. The verbatim report body begins after the rule below. -->

**Primary artifact:** `Library/BugCamEvidence/probe-2.2.3-v1-pins/REPORT.md` (5462 bytes, modified 2026-08-05 09:23).

**That path is gitignored** by `.gitignore` line 2 `/[Ll]ibrary/`, and is therefore unreadable from a clean checkout of this repository.

**This document is a transcription made 2026-08-05, not the primary artifact.** The body below is carried over from the file on disk byte for byte: nothing is rounded, abbreviated, summarised, reordered or corrected, and no analysis of the transcribing agent is added.

**The run is NOT reproducible from this repository:** the probe code was never committed and branch `probe/2.2.3-pins` is deleted.

**Caveat raised by the measuring agent, carried here:** pin 1 goes through `TowerProbeRequestFactory.CreateBaseline` and never enters `SceneCapture.Capture`, which makes pins 2 and 3 the discriminating ones.

---

# Probe 2 variant 1 — live pin re-run (measurement only, 2026-08-05)

Throwaway branch `probe/2.2.3-pins` off `main` `7cf34f6`; deleted after the run. Nothing
committed, pushed, merged; no docs or contract touched. Live editor Unity `6000.3.21f1`,
mcp-for-unity `3.4.5` over `127.0.0.1:8080`.

## Verdict

All three pinned live gates stayed BIT-IDENTICAL with the internal scale channel present.

## Bridge precondition

First port read caught the server mid-start (Unity `19284` in `SYN_SENT`, no listener).
Re-read: PID `14416` LISTENING on `127.0.0.1:8080`, Unity ESTABLISHED to it. Full MCP
handshake completed (`initialize` → `mcp-session-id` → `notifications/initialized` →
`tools/list`); `execute_code` and `run_tests` both present.

## Probe edits (reverted)

`Assets/BugCam/Core/SceneCapture.cs` only, +72/−2:

- `StaticCandidate.SourceLossyScale` (field on the private nested class).
- Populated from `transform.lossyScale` at both construction sites (`ClassifyStaticObject`,
  `ClassifyRigidbodyObject` kinematic branch).
- `MatchStaticScales(List<StaticCandidate>, Dictionary<string, Vector3>)` — row 12 of the
  2.2.3 failure table, zero tolerance, bit-exact via `SingleToInt32Bits`; called from inside
  `SceneCapture.Capture` after the deterministic sort, per adjudication Р1(a). Result
  discarded on every non-snapshot path.
- `SceneCaptureResult`, `ComputeHash` and every public signature untouched.

The stop-and-report branch did not fire: `TryDescribeSupportedCollider` already yields the
scale at both sites and `Capture` owns the `statics` list through to `ComputeHash`, so the
comparison reaches the scale without any public expansion.

## Call site is live, not a dead field

On `GateScene222`, `Capture` with a drifted stand-in snapshot table flagged all four statics
by their real keys (`0000:Floor#00`, `0001:WallBack#00`, `0002:WallArchedFront#00`,
`0003:Stairs#00`); with no drift it returned none. Capture hash identical in both passes —
the channel contributes nothing to the hash whether or not it reports drift. A 1-ULP drift
(`2.0f` vs `2.0000002f`) is caught.

## Pins

| Pin | Run | Result |
|---|---|---|
| 1 tower (factory) | `ghost-20260805T131616647-body49-X-AscendFromStart` | BIT-IDENTICAL |
| 2 domino (captured scene) | `ghost-20260805T131755106-body1-X-AscendFromStart` | BIT-IDENTICAL |
| 3 GateScene222 (edit-mode capture) | in-editor capture | BIT-IDENTICAL |

Pin 1 numbers, all reproduced exactly: threshold `1.98919879E-05`, bracket
`1.978456E-05…1.98919879E-05`, width `1.07427695E-07`, frame 27 (body 49), spread
`0.003794474` (body 5), 21/49, amplification `190.753891`.

Pin 2 numbers, all reproduced exactly: threshold `0.0022972275`, bracket
`0.00228482112…0.0022972275`, frame 3 (body 1), 1/5, amplification `1.00000107`, capture
hash `40e466400a6fd4804a924e0376013782d40d02ffb19b14912d15d0d2bd5b6531` (both in the
edit-mode pre-flight and in the run manifest).

Pin 3: hash `8b8e9a9430f244ded0f409f08cfa5a00bbb7a7442346fbed259a97790244b927`, `meshRef`
set on all 10 objects, 6 bodies + 4 statics, zero warnings.

Structural note: the tower path builds its bodies from `TowerProbeRequestFactory.CreateBaseline`
and never enters `SceneCapture.Capture`, so pin 1 is immune to this edit by construction —
pins 2 and 3 are the discriminating gates.

## Field-diff (metrics.json + manifest.json, every leaf field)

- Tower vs A3 pin `ghost-20260803T161330582…`: 328 + 158 fields, 0 unexpected diffs
  (`builtUtc`, `runId`, `runDirectory`, plus the documented `scenePath` metadata class —
  the pin was recorded with an untitled scene, this run with `DominoScene` open).
- Tower vs 04.08 control `ghost-20260804T074943556…`: 0 unexpected diffs, `scenePath` identical.
- Domino vs A8 pin `ghost-20260803T174400539…`: 284 + 204 fields, 0 unexpected diffs.
- Domino vs 04.08 control `ghost-20260804T074908987…`: 0 unexpected diffs.

## Suites and named pins

- EditMode `157/157` passed, 0 failed, 0 skipped — exactly baseline 157, no growth.
- PlayMode `27/27` passed, 0 failed, 0 skipped — exactly baseline 27, no growth.
- `BugCam.Tests.MeshCaptureTests.SceneCaptureHandoverRoundTripsBitIdentically` — Passed.
- `BugCam.Tests.MeshCaptureTests.ObjectScaleChannelBitIdentityPin` — Passed.

## Source inspection

- `StaticSourceScales`: absent from `Assets/**` — zero occurrences.
- `SceneCaptureResult.Success`: exactly one declaration (`SceneCapture.cs:170`), untouched by
  the diff; no overload added or changed anywhere in Core or Editor.
- `SourceLossyScale`: confined to `SceneCapture.cs`, inside the private nested class and the
  private match method; no public reachability.

## Cleanup

Two known drifts appeared after the PlayMode suite and were both repaired:
`ProjectSettings/EditorSettings.asset` (`m_EnterPlayModeOptions: 0→1`) and
`Assets/BugCam/Tests/TowerScene.unity` (fileID renumbering from the gate tooling).
`EnterPlayModeOptions` was set back to `None` from inside the live editor before the disk
revert. After revert + refresh the live editor assembly carries no probe member
(`ProbeSnapshotScales`, `_probeLastScaleMismatches`, `MatchStaticScales`, `SourceLossyScale`
all absent by reflection); console has zero errors.

Final state: HEAD on `main` `7cf34f6`; `git status --porcelain` shows exactly the four
protected untracked files; `ProjectSettings/` clean; branch `probe/2.2.3-pins` deleted;
only gitignored logs and evidence left behind.
