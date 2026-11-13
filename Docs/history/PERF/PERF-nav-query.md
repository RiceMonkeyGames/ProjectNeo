# PERF-nav-query | 2026-11-12 | Combat/AI - Real AI Benchmark and Search Cost Reduction

> Purpose: Verification & completion log. | Parent: [history/README](../README.md)

### 2026-11-12: PERF-nav-query - Real AI Benchmark and Search Cost Reduction

- Assignee Codex, branch `feature/nav-query-profile`, based on develop claim `55ee79d`. Claim was committed/pushed before implementation. User confirmed the shared `UnitCombatAI.cs` file had no competing edits and authorized changes. Status: Waiting for Verification; feature changes remain uncommitted/unmerged for user review. No remote feature branch has been published or deleted.
- Diagnosis: No-target Hold and Engage treated null as an invalid target each frame and bypassed DecisionInterval. Idle Hold consequently scanned the entire actor registry every frame even when no enemy was within chase range. This baseline's dominant cost was candidate/range validation, not proven to be NavMesh path queries.
- Fix: Only invalidate an existing target immediately (including destroyed Unity object handles); otherwise respect the existing 0.25s decision interval. Reject same-team/out-of-radius candidates early; for Hold in-reach scans use conservative collider AABB distance before the unchanged exact collider/LoS checks. Retain offset/large colliders. Reuse the valid retained Engage target's reachability path instead of calculating it twice. Move path recalculation and terrain mutation handling remain unchanged.
- Added `UnitAIQueryBenchmark.cs` and Editor-generated .meta, plus 3 UnitCombatPlayTests behavioral tests for delayed empty-target acquisition, immediate death replacement (Hold and Engage), and an offset collider whose root is outside chase radius but body is in range.
- Environment: Unity 6000.6.8f1 Editor, Windows 11, Intel Core i7-14700 (28 logical cores). Each scenario has 30 warm-up and 120 measurement frames at fixed simulated 1/30s. Geometry, soldiers, profile, AI settings and NavMesh are created in a temporary scene using existing factories/Editor-managed import; runtime numerical definitions were not changed. No randomness is used.
- Measurement: Automatic AI updates are disabled and cached delegates invoke the unchanged Update entry point once per simulated frame; Stopwatch measures aggregate AI CPU dispatch only. Engine steering/physics/rendering/waiting are excluded; wall elapsed is separate. These are controlled flat-arena Editor measurements, not full-game FPS, path-query counts or standalone guarantees. No timing pass/fail threshold is used.
- Repeatability: Both before baselines and final results passed twice. 200v200 idle Hold mean was 87.287/83.371ms before, 1.680/1.679ms after (98.0% reduction comparing second runs). Final p95 was 11.973/12.023ms; synchronized decision frames still produce peaks. Contact Hold/Engage/Move showed no material regression in these samples. Table uses the second baseline and second final run:

| Case | Per team | Before AI mean ms | After AI mean ms | Before p95 ms | After p95 ms |
|---|---|---|---|---|---|
| Move | 50 | 0.067 | 0.073 | 0.221 | 0.219 |
| HoldIdle | 50 | 5.343 | 0.162 | 5.996 | 0.866 |
| HoldContact | 50 | 0.554 | 0.553 | 0.619 | 0.584 |
| Engage | 50 | 0.341 | 0.339 | 0.538 | 0.483 |
| Move | 200 | 0.257 | 0.249 | 0.805 | 0.785 |
| HoldIdle | 200 | 83.371 | 1.679 | 86.611 | 12.023 |
| HoldContact | 200 | 2.387 | 2.297 | 2.910 | 2.408 |
| Engage | 200 | 2.065 | 2.006 | 2.810 | 2.532 |

- Verification: Compilation `failed=False compiling=False`; focused UnitCombatPlayTests 12/12 passed; full non-explicit PlayMode suite 91/91 passed (37.85s). Explicit benchmark passed twice before and twice after final optimization (final runs 3.06s/3.11s); an intermediate cadence-only benchmark also passed. EditMode was not rerun because no pure C# rule implementation changed. No standalone build or physical input playtest was performed.
- Cleanup verified after tests: `playing=False capture=0 background=False vsync=0 targetRate=-1`; original Game scene restored. Benchmark teardown owns temporary scene/assets and restores settings. Auto-tick was already enabled before execution. `git diff --check` passed.
- Local measurement files: Logs/benchmark_nav_query_baseline1.csv, baseline2.csv, cadence.csv, final1.csv, and benchmark_nav_query.csv (latest final). These Logs artifacts are ignored by Git; the table above preserves evidence in repository docs.
- Manual/repeat verification: In Window > General > Test Runner > PlayMode select UnitCombatPlayTests and Run Selected. Run the explicit benchmark via the exact Context Section 6 command; inspect Logs/benchmark_nav_query.csv after completion. For actual controls use Assets/_Project/Scenes/Game.unity: Play → F1-F3 select → 3 Hold → F at gate banner to start battle → confirm idle/nearby-enemy response, then 2 Engage and 1 Move on aimed ground; targets should be replaced after death and normal wall routes should remain usable. Stop Play to reset. Game scene is open for this check. New unit-count/terrain stress scenarios, standalone profiling and user-owned commit/merge remain outside verified scope.

### 2026-11-12: PERF-nav-query - User Manual Verification Confirmed

- User replied that verification was complete after the supplied Game scene check procedure. This is user-reported confirmation of that check, not an additional agent-run playtest or standalone performance measurement.
- Backlog status changed from Waiting for Verification to Done. ImplementationPlan updated to record the confirmation; prior automated results remain unchanged (PlayMode 91 passed and repeated before/after explicit benchmarks).
- Feature code and supporting docs remain uncommitted on `feature/nav-query-profile`; only the backlog completion status is synchronized to shared develop under the workflow exception. User-owned feature commit/merge remains pending. The remote feature branch does not exist and is not eligible for cleanup before integration.
- Existing repeat/manual steps remain in Context Section 6. Stop Play to reset runtime state. No new Unity operations were needed for this documentation-only confirmation.
- Backlog-only completion commit `df81940` was pushed to remote `develop` from an isolated clean worktree; latest teammate commits were preserved. Feature code/docs remain untouched and uncommitted in the original worktree.

### 2026-11-12: PERF-nav-query - Final Integration at User Request

- User requested finalization, authorizing feature commit/push, develop integration and task branch cleanup. Implementation commit `22061ac`; latest develop merged into feature as `03c57df`, preserving both documentation histories and teammate access-policy changes. Feature branch published; local develop merge `f7207f3`. A concurrent teammate backlog-only update `251b8e5` was then integrated, preserving other assignees' Done statuses; no additional gameplay changes required another code regression run.
- Integrated regression: An initial attempt launched from the user's still-active PlayMode stalled in transition, was cancelled, and restarted only after `playing=False transitioning=False`. Under default unfocused input routing the full suite finished 88/91: the existing GameEquipment I-open, GameFacility F-open and GameFlow construction F-open tests failed. AI, movement and siege tests passed.
- Diagnosis/check: Focus-independent synthetic input routing (InputSettings IgnoreFocus + AllDeviceInputAlwaysGoesToGameView, background execution on) made GameEquipmentPlayTests pass 3/3 and the full suite pass 91/91 (38.15s). No input/product code was altered. Registered BUG-playmode-input-focus for test fixture isolation; this follow-up is separate from AI optimization.
- Cleanup: Original input routing and background settings restored via Editor APIs. Final restored values: playing=False, background=False, ResetAndDisableNonBackgroundDevices, PointersAndKeyboardsRespectGameViewFocus. Saved restored PlayerSettings through Editor APIs and verified clean git status before merge. No temporary project-setting change is included in the feature.
- Existing user manual confirmation remains valid; benchmark timing is the earlier repeated flat-arena Editor AI-only measurement, not standalone FPS. No further build or physical input test was claimed. Repeat/manual procedure remains Context Section 6, including Stop Play to reset.
- Remote develop push confirmed at `59c262d`. Verified the feature tip was an ancestor of origin/develop, then deleted remote `feature/nav-query-profile` and its merged local branch. Current branch is develop and the working tree was clean. Feature code and supporting docs are now integrated, rather than only backlog status.
