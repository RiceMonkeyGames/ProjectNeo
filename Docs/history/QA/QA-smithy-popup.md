# QA-smithy-popup | 2026-11-14 | Prep - Manual Check of Blacksmith Crafting & Sales

Last Updated: 2026-11-14 14:02:41 (KST)

> Purpose: Verification & completion log. | Parent: [history/README](../README.md) | Related: [D-6](../D/D-6.md), [PrepEconomyMeta](../../design/PrepEconomyMeta.md)

### 2026-11-14: QA-smithy-popup - Manual Check of Blacksmith Crafting & Sales

- Assignee: Hwang Seong-min. Branch `feature/qa-smithy-popup` from develop `51de986` (claim pushed first). Follow-up of D-6, whose manual play was skipped on 2026-11-13.
- Before the check: smithy popup code (`GameFacilities.TryCraft`, `TrySellEquipment`) and D-6 data were reviewed. Teammates' PREP-02/PREP-03 shop changes merged earlier did not touch the smithy path.
- Manual play (user, `Assets/_Project/Scenes/Game.unity`, Editor Play, preparation phase), all passed:
  1. F on Smithy opens the popup.
  2. Lv1 lists 3 recipes (wood shield, iron sword, leather armor); the sell column shows the "no equipment" line.
  3. Iron sword without hide shows "비용이 모자랍니다" and gold is unchanged.
  4. Crafting the wood shield takes gold 100 → 80; the recipe stays listed.
  5. Selling the shield gives +12 gold.
  6. Consumables bought at the Shop are not listed for sale at the Smithy.
  7. Upgrading the Smithy to Lv2 lists 4 recipes.
- No code, scene, or asset changes. No Unity automated runs for this QA task. Standalone build not verified.
- User reported all checks passed and asked to finish on 2026-11-14.
