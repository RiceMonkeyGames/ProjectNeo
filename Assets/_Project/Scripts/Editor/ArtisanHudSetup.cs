using System;
using ProjectNeo.Flow;
using ProjectNeo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// PREP-11 artisan HUD, Game scene only. Adds the "장인 N/Max" label to the sidebar prep clock row and rebuilds the
    /// construction bar with the work tab (option B, user 2026-11-25) without rerunning the whole preparation HUD layout.
    /// Both pieces are also part of their full menus (sidebar layout and construction bar), so later reruns keep them.
    /// Running it again gives the same result.
    /// </summary>
    public static class ArtisanHudSetup
    {
        [MenuItem("ProjectNeo/Setup/Artisan HUD (PREP-11)")]
        public static void ApplyToActiveScene()
        {
            var hud = UnityEngine.Object.FindAnyObjectByType<GameFlowHud>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("Game HUD is required.");
            var construction = UnityEngine.Object.FindAnyObjectByType<ConstructionHud>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("Construction HUD is required.");
            PrepHudSidebarLayout.ArtisanLabel(hud);
            ConstructionBarLayout.Apply(construction);
            EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
            EditorSceneManager.SaveScene(hud.gameObject.scene);
        }
    }
}
