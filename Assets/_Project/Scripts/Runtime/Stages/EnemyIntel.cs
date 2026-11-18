using System.Collections.Generic;
using System.Text;

namespace ProjectNeo.Stages
{
    /// <summary>
    /// 다음(또는 지금) 전투의 적 정보(C-5, Q-53). 스테이지 전체의 종류별 수를 보여 주고, 숨긴 무리는 종류 없이 "? × 수"로 보인다.
    /// 숨긴 무리는 공개(전장 등장, 이후의 정찰)되면 종류가 드러난다. 남은 수는 바깥(전투)에서 넣어 준다.
    /// 프리팹·씬·시간을 모르는 순수 규칙이다.
    /// </summary>
    public sealed class EnemyIntel
    {
        readonly EnemyIntelGroup[] groups;
        readonly int[] remaining;
        readonly bool[] revealed;
        readonly List<EnemyIntelRow> rows = new List<EnemyIntelRow>();
        bool rowsDirty = true;

        public EnemyIntel(IReadOnlyList<EnemyIntelGroup> source)
        {
            int count = source != null ? source.Count : 0;
            groups = new EnemyIntelGroup[count];
            remaining = new int[count];
            revealed = new bool[count];
            for (int i = 0; i < count; i++)
            {
                groups[i] = source[i];
                remaining[i] = source[i].Count;
            }
        }

        /// <summary>편성의 웨이브 순서, 그 안의 무리 순서로 무리 번호를 매긴다. 종류 ID는 프리팹 이름이다.</summary>
        public static EnemyIntel FromPlan(EncounterPlan plan)
        {
            var list = new List<EnemyIntelGroup>();
            if (plan != null)
                foreach (EncounterWave wave in plan.Waves)
                    foreach (EncounterGroup group in wave.Groups)
                    {
                        string id = group.Prefab != null ? group.Prefab.name : string.Empty;
                        list.Add(new EnemyIntelGroup(id, DisplayName(id), group.Count, group.Hidden));
                    }
            return new EnemyIntel(list);
        }

        public int GroupCount => groups.Length;
        /// <summary>보이는 내용이 바뀔 때마다 오른다. 화면은 이 값이 바뀔 때만 다시 그리면 된다.</summary>
        public int Version { get; private set; }

        public EnemyIntelGroup Group(int index) => groups[index];
        public int RemainingOf(int index) => remaining[index];
        /// <summary>종류가 보이는 무리인가. 숨기지 않았거나 이미 공개됐으면 true.</summary>
        public bool IsKnown(int index) => !groups[index].Hidden || revealed[index];

        /// <summary>아직 종류가 숨겨진 무리가 있는가. 정찰을 제안할지 정할 때 쓴다.</summary>
        public bool AnyHidden
        {
            get
            {
                for (int i = 0; i < groups.Length; i++) if (!IsKnown(i)) return true;
                return false;
            }
        }

        public int TotalRemaining
        {
            get
            {
                int total = 0;
                foreach (int value in remaining) total += value;
                return total;
            }
        }

        /// <summary>무리 하나의 종류를 공개한다(전장 등장). 이미 보이면 바뀌지 않는다.</summary>
        public bool Reveal(int index)
        {
            if (index < 0 || index >= groups.Length || IsKnown(index)) return false;
            revealed[index] = true;
            Touch();
            return true;
        }

        /// <summary>숨겨진 무리를 모두 공개한다(정찰). 공개된 무리 수를 돌려준다.</summary>
        public int RevealAll()
        {
            int count = 0;
            for (int i = 0; i < groups.Length; i++)
                if (!IsKnown(i)) { revealed[i] = true; count++; }
            if (count > 0) Touch();
            return count;
        }

        /// <summary>무리 하나의 남은 수를 넣는다. 0과 전체 수 사이로 맞춘다.</summary>
        public void SetRemaining(int index, int value)
        {
            if (index < 0 || index >= groups.Length) return;
            int clamped = value < 0 ? 0 : value > groups[index].Count ? groups[index].Count : value;
            if (remaining[index] == clamped) return;
            remaining[index] = clamped;
            Touch();
        }

        /// <summary>
        /// 보여 줄 줄들. 보이는 무리는 같은 종류끼리 한 줄로 합치고(처음 나온 순서), 숨긴 무리는 그 뒤에
        /// 종류별로 "?" 줄 하나씩 둔다. 남은 수가 0이 된 줄도 남긴다.
        /// </summary>
        public IReadOnlyList<EnemyIntelRow> Rows()
        {
            if (!rowsDirty) return rows;
            rows.Clear();
            AddRows(true);
            AddRows(false);
            rowsDirty = false;
            return rows;
        }

        void AddRows(bool known)
        {
            var order = new List<string>();
            var left = new Dictionary<string, int>();
            var total = new Dictionary<string, int>();
            var names = new Dictionary<string, string>();
            for (int i = 0; i < groups.Length; i++)
            {
                if (IsKnown(i) != known) continue;
                string id = groups[i].TypeId;
                if (!total.ContainsKey(id)) { order.Add(id); total[id] = 0; left[id] = 0; names[id] = groups[i].TypeName; }
                total[id] += groups[i].Count;
                left[id] += remaining[i];
            }
            foreach (string id in order) rows.Add(new EnemyIntelRow(names[id], left[id], total[id], !known));
        }

        /// <summary>여러 줄 글자. 적이 없으면 "No enemies".</summary>
        public string Describe()
        {
            IReadOnlyList<EnemyIntelRow> list = Rows();
            if (list.Count == 0) return "No enemies";
            var text = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append(list[i].Describe());
            }
            return text.ToString();
        }

        /// <summary>프리팹 이름을 보여 줄 이름으로 바꾼다. 예: "enemy_gate_breaker" → "Gate Breaker".</summary>
        public static string DisplayName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return "Unknown";
            string name = prefabName;
            if (name.StartsWith("enemy_", System.StringComparison.OrdinalIgnoreCase)) name = name.Substring(6);
            string[] words = name.Replace('-', '_').Split(new[] { '_', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return prefabName;
            for (int i = 0; i < words.Length; i++)
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        void Touch()
        {
            rowsDirty = true;
            Version++;
        }
    }
}
