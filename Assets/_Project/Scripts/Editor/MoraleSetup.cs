using ProjectNeo.Flow;
using ProjectNeo.Morale;
using UnityEditor;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// B-8.2 아군 부대 사기. 사기 설정 에셋을 만들고 Game 씬의 전투 설정(Battle.asset)에 넣는다.
    /// 씬 파일은 바꾸지 않는다(GameBattleController가 실행 중에 사기 컴포넌트를 붙인다). 여러 번 실행해도 같다.
    /// </summary>
    public static class MoraleSetup
    {
        public const string MoralePath = "Assets/_Project/Data/GameFlow/Morale.asset";
        const string BattlePath = "Assets/_Project/Data/GameFlow/Battle.asset";

        [MenuItem("ProjectNeo/Setup/Connect Morale To Game Battle")]
        public static void Apply()
        {
            var battle = AssetDatabase.LoadAssetAtPath<GameBattleSettings>(BattlePath);
            if (battle == null) throw new System.InvalidOperationException("Battle settings are missing: " + BattlePath);
            Connect(battle);
            AssetDatabase.SaveAssets();
        }

        /// <summary>사기 설정 에셋이 없으면 기본값으로 만들고 전투 설정에 넣는다.</summary>
        public static MoraleSettings Connect(GameBattleSettings battle)
        {
            MoraleSettings morale = CombatPrototypeSetup.Save(MoraleSettings.Create(), MoralePath);
            var data = new SerializedObject(battle);
            SerializedProperty field = data.FindProperty("morale");
            if (field.objectReferenceValue != morale)
            {
                field.objectReferenceValue = morale;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(battle);
            }
            return morale;
        }
    }
}
