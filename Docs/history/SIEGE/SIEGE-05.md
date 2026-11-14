# SIEGE-05 | 2026-11-14 | Siege - Ladder Push Weight & Cooperation

Last Updated: 2026-11-14 17:27:30 (KST)

Branch: `feature/siege-05`. Owner: Hwang Seong-min. Claim synchronized to develop (`docs: claim SIEGE-05`). After the implementation, test results and manual verification guide were presented, the user authorized completion/archive/commit/push on 2026-11-14; develop merge remains with the user. Design record: [WallsAndSiege](../../design/WallsAndSiege.md) (Shared ladder traversal → Push weight & cooperation).

## Decision (user, 2026-11-14)

User feedback: the fixed 2s push felt too fast. In practice the hold was even shorter because the ladder auto-completes after crossing the vertical balance point, so only `2s × (1 − balance)` had to be held.

| Item | Choice |
|---|---|
| ① Base time | Keep auto-complete after balance; one pusher on an empty ladder must hold **3s** until balance. Raise stays 3s. |
| ② Weight | One occupant (ally or enemy; one-at-a-time rule unchanged) multiplies hold time by fixed **×1.5**. |
| ③ Cooperation | Allies pushing together at the upper endpoint: 2 → **+50%**, 3 or more → **+75%** (cap). |
| ④ Upper-exit holding | **Excluded**; separate task if wanted (conflicts with Q-41 "no attacks while on ladder" and touches enemy assault code). |

Rejected alternatives: requiring the hold until fully toppled (reverses the approved balance auto-complete), multi-occupant ladders (reopens Q-41 traffic), tug-of-war against enemies at the base (teammate-owned enemy AI).

## Implemented scope

- `LadderPushTuning` (pure C#): hold seconds × occupied multiplier ÷ (1 + cooperation bonus). Bonus never shrinks with more pushers; multiplier never below 1.
- `SiegeLadderState`: new `Tick(..., int pusherCount, ...)`; bool overload kept (= 1 pusher). Pre-balance push rate follows `PushToBalanceSeconds(pushers)`; post-balance settle and pre-balance release return keep the `pushSeconds` (2s full rotation) rate. Without tuning the old behavior is unchanged.
- `SiegeSettings`: `ladderPushHoldSeconds` 3, `ladderOccupiedPushMultiplier` 1.5, `ladderTwoPusherBonus` 0.5, `ladderThreePlusPusherBonus` 0.75, plus `CreateLadderPushTuning()`. Existing `.asset` files were not edited; new fields use their code defaults.
- `SiegeLadder.FindPushers` counts eligible pushers (player holding F + idle allied AI) instead of a yes/no flag. Friendly-use protection and nearby-enemy blocking unchanged.

## Verification

Unity compile passed. Full `unity-verify-local.ps1 -Mode all -SkipDotnet` passed EditMode **393/393** (3.2s) and PlayMode **183/183** (107.7s). New EditMode cases: `LadderPushTuning_HoldTimeFollowsWeightAndCooperation` (6), `LadderPushTuning_NoPusher_NeverReachesBalance_AndBonusNeverShrinks`, `Ladder_TunedPush_ReachesBalanceOnlyAfterHoldTime` (4), `Ladder_TunedPush_ReleasedBeforeBalance_ReturnsAtSettleSpeed`. Existing shared-ladder PlayMode cases now derive push time from `SiegeLadderState.PushToBalanceSeconds(1)` and pass with the new 3s / ×1.5 timings, including the autonomous enemy-ladder topple integration case.

Dotnet precheck was skipped: the Unity-generated `.csproj` files are stale (missing `LadderPushTuning` and teammates' newer Economy types), so its failure is not a code error. This is not a separate dotnet verification claim.

Physical-key manual play (player hold time feel, cooperation with idle allies) is not claimed as verified.
