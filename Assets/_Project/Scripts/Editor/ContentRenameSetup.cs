using ProjectNeo.Core;
using ProjectNeo.Meta;
using ProjectNeo.Run;
using UnityEditor;
using UnityEngine;

namespace ProjectNeo.Editor
{
    /// <summary>
    /// 콘텐츠 ID 이름 바꾸기 표(META-10)를 만들고 런 콘텐츠 목록과 해금 목록에 같은 에셋으로 잇는다.
    /// 표가 이미 있으면 내용은 그대로 둔다. 두 목록을 다시 만드는 설정 스크립트도 끝에 이것을 부른다.
    /// </summary>
    public static class ContentRenameSetup
    {
        public const string TablePath = "Assets/_Project/Data/GameFlow/ContentRenames.asset";

        [MenuItem("ProjectNeo/Setup/Link Content Rename Table (META-10)")]
        public static void Apply()
        {
            ContentRenameTable table = Link();
            AssetDatabase.SaveAssets();
            foreach (string problem in table.Validate()) Debug.LogError("Content rename table: " + problem, table);
        }

        /// <summary>표를 만들거나 불러와 두 목록에 잇는다. 저장(SaveAssets)은 부르는 쪽이 한다.</summary>
        public static ContentRenameTable Link()
        {
            var table = AssetDatabase.LoadAssetAtPath<ContentRenameTable>(TablePath);
            if (table == null)
            {
                table = ContentRenameTable.Create();
                AssetDatabase.CreateAsset(table, TablePath);
            }
            var content = AssetDatabase.LoadAssetAtPath<RunContentCatalog>(RunSaveSetup.CatalogPath);
            if (content != null && content.Renames != table)
            {
                content.SetRenames(table);
                EditorUtility.SetDirty(content);
            }
            var unlocks = AssetDatabase.LoadAssetAtPath<UnlockCatalog>(UnlockCatalogSetup.CatalogPath);
            if (unlocks != null && unlocks.Renames != table)
            {
                unlocks.SetRenames(table);
                EditorUtility.SetDirty(unlocks);
            }
            return table;
        }
    }
}
