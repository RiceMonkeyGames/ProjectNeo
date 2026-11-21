using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectNeo.Squads
{
    public enum SquadOrderKind { Hold = 0, Move = 1, Attack = 2 }

    /// <summary>
    /// 부대 하나에 내린 지시. 이동은 목적지로 가고, 목적지가 있는 공격은 그곳으로 가면서 발견한 적과 싸운다.
    /// 목적지가 없는 공격은 지금 자리에서 시야에 들어온 적과 싸운다.
    /// </summary>
    public readonly struct SquadOrder
    {
        public SquadOrderKind Kind { get; }
        public bool HasDestination { get; }
        public Vector3 Destination { get; }
        public float Facing { get; }
        /// <summary>
        /// 목적지가 앞줄 가운데다(COMBAT-08 드래그로 그은 선). 아니면 목적지가 대형 가운데다(클릭·키 이동).
        /// </summary>
        public bool FrontAnchored { get; }
        public SquadOrder(SquadOrderKind kind)
        {
            Kind = kind; HasDestination = false; Destination = default; Facing = 0f; FrontAnchored = false;
        }
        public SquadOrder(SquadOrderKind kind, Vector3 destination, float facing = 0f, bool frontAnchored = false)
        {
            Kind = kind; HasDestination = true; Destination = destination; Facing = facing; FrontAnchored = frontAnchored;
        }
    }

    /// <summary>
    /// 부대 선택과 지시 상태. 씬·입력·유닛은 알지 않는다.
    /// 기본 상태는 중지다. 이동은 도착을 알리면 중지로 바뀌고, 공격은 새 지시가 올 때까지 유지한다.
    /// </summary>
    public sealed class SquadCommander
    {
        SquadOrder[] orders;
        bool[] selected;
        FormationKind[] formations;
        float[] widths;
        public int SquadCount => orders.Length;
        /// <summary>부대의 진형이나 전열 폭이 바뀌었다(COMBAT-08). 인자는 부대 번호.</summary>
        public event Action<int> FormationChanged;
        /// <summary>지시가 바뀐 부대 번호와 새 지시.</summary>
        public event Action<int, SquadOrder> OrderChanged;
        /// <summary>새 지시를 부대별로 한 번 심사한다. 하나라도 false면 기존 지시를 유지한다. 도착 처리는 심사하지 않는다.</summary>
        public event Func<int, SquadOrder, bool> OrderRequested;
        public event Action SelectionChanged;
        /// <summary>부대가 늘었다. 인자는 새 부대 번호.</summary>
        public event Action<int> SquadAdded;

        public SquadCommander(int squadCount)
        {
            if (squadCount < 0) throw new ArgumentOutOfRangeException(nameof(squadCount));
            orders = new SquadOrder[squadCount];
            selected = new bool[squadCount];
            formations = new FormationKind[squadCount];
            widths = new float[squadCount];
            for (int i = 0; i < squadCount; i++) widths[i] = DefaultWidth;
        }

        /// <summary>전열 폭을 정한 적 없음. 지휘 설정의 기본 한 줄 인원으로 선다.</summary>
        public const float DefaultWidth = -1f;

        /// <summary>
        /// 맨 뒤에 부대를 하나 더하고 번호를 돌려준다(D-0b 모집으로 새 부대가 생길 때). 새 부대는 중지 상태이고 선택되지 않는다.
        /// </summary>
        public int AddSquad()
        {
            int index = orders.Length;
            Array.Resize(ref orders, index + 1);
            Array.Resize(ref selected, index + 1);
            Array.Resize(ref formations, index + 1);
            Array.Resize(ref widths, index + 1);
            widths[index] = DefaultWidth;
            SquadAdded?.Invoke(index);
            return index;
        }

        /// <summary>부대가 줄었다. 인자는 없앤 부대의 옛 번호이며, 뒤의 부대 번호는 하나씩 당겨진다.</summary>
        public event Action<int> SquadRemoved;

        /// <summary>
        /// 부대 하나를 없앤다(D-8 편성으로 빈 부대가 생길 때). 뒤의 부대는 번호가 하나씩 당겨지고 지시·선택은 따라간다.
        /// 없는 번호면 false.
        /// </summary>
        public bool RemoveSquad(int squad)
        {
            if (squad < 0 || squad >= orders.Length) return false;
            bool wasSelected = selected[squad];
            for (int i = squad; i < orders.Length - 1; i++)
            {
                orders[i] = orders[i + 1];
                selected[i] = selected[i + 1];
                formations[i] = formations[i + 1];
                widths[i] = widths[i + 1];
            }
            Array.Resize(ref orders, orders.Length - 1);
            Array.Resize(ref selected, selected.Length - 1);
            Array.Resize(ref formations, formations.Length - 1);
            Array.Resize(ref widths, widths.Length - 1);
            SquadRemoved?.Invoke(squad);
            if (wasSelected) SelectionChanged?.Invoke();
            return true;
        }

        public SquadOrder OrderOf(int squad) => orders[squad];
        public FormationKind FormationOf(int squad) => formations[squad];
        /// <summary>전열 폭(m). 정한 적 없으면 <see cref="DefaultWidth"/>.</summary>
        public float WidthOf(int squad) => widths[squad];
        public bool HasWidth(int squad) => widths[squad] >= 0f;

        /// <summary>부대의 진형을 바꾼다. 폭(m)은 그대로 두므로 간격이 바뀌면 한 줄 인원이 다시 계산된다. 없는 번호면 false.</summary>
        public bool SetFormation(int squad, FormationKind kind)
        {
            if (squad < 0 || squad >= formations.Length) return false;
            if (formations[squad] == kind) return true;
            formations[squad] = kind;
            FormationChanged?.Invoke(squad);
            return true;
        }

        /// <summary>부대의 전열 폭(m)을 정한다. 음수면 기본 폭으로 되돌린다. 없는 번호면 false.</summary>
        public bool SetWidth(int squad, float width)
        {
            if (squad < 0 || squad >= widths.Length) return false;
            width = width < 0f ? DefaultWidth : width;
            if (Mathf.Approximately(widths[squad], width)) return true;
            widths[squad] = width;
            FormationChanged?.Invoke(squad);
            return true;
        }

        /// <summary>다음 진형(횡대→밀집→산개→종대→횡대).</summary>
        public static FormationKind Next(FormationKind kind) => kind switch
        {
            FormationKind.Line => FormationKind.Close,
            FormationKind.Close => FormationKind.Loose,
            FormationKind.Loose => FormationKind.Column,
            _ => FormationKind.Line,
        };
        public bool IsSelected(int squad) => squad >= 0 && squad < selected.Length && selected[squad];

        public int SelectedCount
        {
            get { int count = 0; foreach (bool value in selected) if (value) count++; return count; }
        }

        /// <summary>부대 하나만 선택한다. 없는 번호면 선택을 바꾸지 않고 false.</summary>
        public bool Select(int squad)
        {
            if (squad < 0 || squad >= selected.Length) return false;
            for (int i = 0; i < selected.Length; i++) selected[i] = i == squad;
            SelectionChanged?.Invoke();
            return true;
        }

        public void SelectAll()
        {
            for (int i = 0; i < selected.Length; i++) selected[i] = true;
            SelectionChanged?.Invoke();
        }

        public void ClearSelection()
        {
            for (int i = 0; i < selected.Length; i++) selected[i] = false;
            SelectionChanged?.Invoke();
        }

        /// <summary>선택과 상관없이 부대 하나에 지시한다(COMBAT-08 선을 나눠 받는 부대, 진형 바꾸기). 받아들였으면 true.</summary>
        public bool IssueTo(int squad, SquadOrder order)
        {
            if (squad < 0 || squad >= orders.Length || !AcceptOrder(squad, order)) return false;
            orders[squad] = order;
            OrderChanged?.Invoke(squad, order);
            return true;
        }

        /// <summary>선택한 부대 전부에 같은 지시를 내리고, 지시받은 부대 수를 돌려준다.</summary>
        public int Issue(SquadOrder order)
        {
            int count = 0;
            for (int i = 0; i < orders.Length; i++)
            {
                if (!selected[i] || !AcceptOrder(i, order)) continue;
                orders[i] = order;
                count++;
                OrderChanged?.Invoke(i, order);
            }
            return count;
        }

        bool AcceptOrder(int squad, SquadOrder order)
        {
            if (OrderRequested == null) return true;
            foreach (Func<int, SquadOrder, bool> filter in OrderRequested.GetInvocationList())
                if (!filter(squad, order)) return false;
            return true;
        }

        /// <summary>이동 중인 부대가 도착했음을 알린다. 그 자리의 중지 상태가 된다.</summary>
        public bool CompleteMove(int squad)
        {
            if (squad < 0 || squad >= orders.Length || orders[squad].Kind != SquadOrderKind.Move) return false;
            orders[squad] = new SquadOrder(SquadOrderKind.Hold, orders[squad].Destination, orders[squad].Facing,
                orders[squad].FrontAnchored);
            // 도착한 자리의 중지다. 목적지는 "어디에 서 있는가"를 알려주는 기록으로 남긴다.
            OrderChanged?.Invoke(squad, orders[squad]);
            return true;
        }
    }

    public static class SquadFormation
    {
        /// <summary>목적지를 중심으로 한 줄 단위 대형의 자리. 앞줄부터 채우고 각 줄은 가운데 정렬한다.</summary>
        public static List<Vector3> Slots(Vector3 center, float facing, int count, int columns, float spacing) =>
            FormationLayout.Slots(center, facing, count, columns, spacing);
    }
}
