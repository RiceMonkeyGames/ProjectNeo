using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Core
{
    /// <summary>
    /// 콘텐츠 ID 이름 바꾸기 표(META-10, 사용자 결정 2026-11-21). 업데이트에서 아이템·영웅·퍽 등의 ID를 바꾸면
    /// 옛 ID와 새 ID를 여기 적는다. 런 저장과 메타 저장을 되살리기 전에 옛 ID를 새 ID로 바꾼다.
    /// 여러 번 바뀐 ID(a -> b -> c)는 끝까지 따라간다. 표 하나를 런 콘텐츠 목록과 해금 목록이 같이 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectNeo/Content Rename Table", fileName = "ContentRenames")]
    public sealed class ContentRenameTable : ScriptableObject
    {
        // 이름 바꾸기가 서로 물려 끝없이 도는 실수(a -> b -> a)를 막는 상한.
        const int MaxHops = 32;

        [SerializeField] List<ContentRename> renames = new List<ContentRename>();

        public IReadOnlyList<ContentRename> Renames => renames;

        public static ContentRenameTable Create(params ContentRename[] entries)
        {
            var table = CreateInstance<ContentRenameTable>();
            if (entries != null) table.renames.AddRange(entries);
            return table;
        }

        /// <summary>지금 쓰는 ID. 바뀐 적이 없으면 그대로 돌려준다. 비었으면 그대로다.</summary>
        public string Resolve(ContentKind kind, string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            string current = id;
            for (int hop = 0; hop < MaxHops; hop++)
            {
                string next = Next(kind, current);
                if (next == null || next == current) return current;
                current = next;
            }
            return current;
        }

        string Next(ContentKind kind, string id)
        {
            foreach (ContentRename rename in renames)
                if (rename.kind == kind && rename.oldId == id && !string.IsNullOrEmpty(rename.newId)) return rename.newId;
            return null;
        }

        /// <summary>데이터 실수: 빈 ID, 같은 옛 ID가 두 번, 되돌아오는 고리. 없으면 빈 목록.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<(ContentKind, string)>();
            foreach (ContentRename rename in renames)
            {
                if (string.IsNullOrEmpty(rename.oldId) || string.IsNullOrEmpty(rename.newId))
                    problems.Add($"{rename.kind} rename has an empty id.");
                else if (!seen.Add((rename.kind, rename.oldId)))
                    problems.Add($"{rename.kind} id '{rename.oldId}' is renamed twice.");
            }
            foreach (ContentRename rename in renames)
            {
                if (string.IsNullOrEmpty(rename.oldId)) continue;
                string current = rename.oldId;
                for (int hop = 0; hop <= MaxHops; hop++)
                {
                    current = Next(rename.kind, current);
                    if (current == null) break;
                    if (current == rename.oldId) { problems.Add($"{rename.kind} id '{rename.oldId}' renames back to itself."); break; }
                }
            }
            return problems;
        }
    }
}
