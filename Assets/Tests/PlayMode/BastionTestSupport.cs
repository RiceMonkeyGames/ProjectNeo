using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectNeo.UI;
using ProjectNeo.Walls;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectNeo.Tests
{
    /// <summary>
    /// 수성 무기는 보루에만 놓는다(SIEGE-11). 무기를 시험하는 PlayMode 시험이 성벽 옆에 보루를 지어 받침으로 쓰게 돕는다.
    /// 보루 조각은 씬의 건설 화면이 들고 있는 실제 에셋을 쓴다.
    /// </summary>
    internal static class BastionTestSupport
    {
        public static BuildPieceDefinition Definition(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ConstructionHud hud in root.GetComponentsInChildren<ConstructionHud>(true))
                    if (hud.Bastion != null) return hud.Bastion;
                foreach (SiegeHud hud in root.GetComponentsInChildren<SiegeHud>(true))
                    if (hud.Bastion != null) return hud.Bastion;
            }
            Assert.Fail("No bastion piece is linked to a construction HUD in " + scene.name
                + ". Run ProjectNeo/Setup/Apply Bastions (SIEGE-11).");
            return null;
        }

        /// <summary>
        /// 그 성벽 조각 옆 성 바깥 칸에 보루를 짓는다. <paramref name="preferred"/> 방향을 먼저 해 본다. 못 지으면 null.
        /// <paramref name="outward"/>는 성벽에서 보루로 가는 방향의 <see cref="BattlementRules.Directions"/> 순번이며,
        /// 무기를 그 값만큼 돌려 놓으면 성벽 반대쪽(바깥)을 본다.
        /// </summary>
        public static PlacedStructure PlaceBeside(ConstructionWorld world, BuildPieceDefinition bastion, PlacedStructure wall,
            out int outward, Vector2Int? preferred = null, bool onlyPreferred = false)
        {
            outward = -1;
            var directions = new List<Vector2Int>();
            if (preferred.HasValue) directions.Add(preferred.Value);
            if (!onlyPreferred || !preferred.HasValue)
                directions.AddRange(BattlementRules.Directions.Where(direction => !directions.Contains(direction)));
            foreach (Vector2Int direction in directions)
            {
                Vector2Int cell = wall.Placement.Cell + direction;
                if (world.CheckPlacement(bastion, cell, 0) != PlacementIssue.None) continue;
                if (!world.TryPlace(bastion, cell, 0, out PlacedStructure body)) continue;
                outward = BattlementRules.DirectionIndex(direction);
                return body;
            }
            return null;
        }

        /// <summary>
        /// <paramref name="outward"/>(격자 방향) 쪽으로 가장 바깥에 있는 땅 위 성벽 앞에 보루를 짓는다. 사다리 자리는 건너뛴다.
        /// </summary>
        public static PlacedStructure PlaceInFrontOfOutermostWall(ConstructionWorld world, BuildPieceDefinition bastion,
            Vector2Int outward, System.Func<PlacedStructure, bool> wallFilter = null)
        {
            foreach (PlacedStructure wall in world.Structures.Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall
                    && piece.Placement.Level == 0 && (wallFilter == null || wallFilter(piece)))
                .OrderByDescending(piece => Vector2.Dot(piece.Placement.Cell, outward)).ToArray())
            {
                PlacedStructure placed = PlaceBeside(world, bastion, wall, out _, outward, true);
                if (placed != null) return placed;
            }
            Assert.Fail("No wall front is free for a bastion.");
            return null;
        }

        /// <summary>땅 위 성벽 옆에서 지금 보루를 지을 수 있는 칸을 찾는다(짓지는 않는다).</summary>
        public static bool TryFindSpot(ConstructionWorld world, BuildPieceDefinition bastion, out Vector2Int cell)
        {
            foreach (PlacedStructure wall in world.Structures.Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall
                && piece.Placement.Level == 0).OrderBy(piece => piece.Placement.Id))
                foreach (Vector2Int direction in BattlementRules.Directions)
                {
                    cell = wall.Placement.Cell + direction;
                    if (world.CheckPlacement(bastion, cell, 0) == PlacementIssue.None) return true;
                }
            cell = default;
            return false;
        }

        /// <summary>서로 다른 땅 위 성벽 옆에 보루를 <paramref name="count"/>개 짓는다.</summary>
        public static PlacedStructure[] PlaceBesideWalls(ConstructionWorld world, BuildPieceDefinition bastion, int count)
        {
            var result = new List<PlacedStructure>();
            foreach (PlacedStructure wall in world.Structures.Where(piece => piece.Placement.Definition.Kind == BuildPieceKind.Wall
                && piece.Placement.Level == 0).OrderBy(piece => piece.Placement.Id).ToArray())
            {
                if (result.Count >= count) break;
                PlacedStructure placed = PlaceBeside(world, bastion, wall, out _);
                if (placed != null) result.Add(placed);
            }
            Assert.AreEqual(count, result.Count, "Not enough wall faces for bastions.");
            return result.ToArray();
        }
    }
}
