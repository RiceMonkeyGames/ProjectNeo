using System;
using System.Collections.Generic;
using ProjectNeo.Morale;

namespace ProjectNeo.UI
{
    /// <summary>
    /// 준비 화면 분대 카드 페이지 규칙(UI-12). 한 페이지에 perPage장씩 차례로 나누고, [ ] 키로 넘기며(끝에서 처음으로 돈다),
    /// 다른 페이지에 사기가 낮은 분대가 있으면 알린다. 화면과 입력은 모른다.
    /// </summary>
    public static class SquadCardPages
    {
        /// <summary>페이지 수. 분대가 없어도 1.</summary>
        public static int PageCount(int count, int perPage) =>
            perPage <= 0 || count <= perPage ? 1 : (count + perPage - 1) / perPage;

        public static int PageOf(int index, int perPage) => perPage <= 0 || index < 0 ? 0 : index / perPage;

        public static bool OnPage(int index, int page, int perPage) => PageOf(index, perPage) == page;

        /// <summary>delta만큼 넘긴 페이지. 처음과 끝은 서로 이어진다.</summary>
        public static int Step(int page, int delta, int pageCount)
        {
            if (pageCount <= 1) return 0;
            int next = (page + delta) % pageCount;
            return next < 0 ? next + pageCount : next;
        }

        public static int Clamp(int page, int pageCount) => Math.Max(0, Math.Min(page, Math.Max(1, pageCount) - 1));

        /// <summary>
        /// 이번에 새로 선택된 분대가 지금 페이지에 하나도 없으면 그중 첫 분대의 페이지, 아니면 지금 페이지.
        /// 선택 키(F7 등)를 누르면 그 분대가 보이게 한다.
        /// </summary>
        public static int FollowSelection(int page, int perPage, IReadOnlyList<bool> before, IReadOnlyList<bool> now)
        {
            int first = -1;
            for (int i = 0; i < now.Count; i++)
            {
                bool added = now[i] && !(i < before.Count && before[i]);
                if (!added) continue;
                if (OnPage(i, page, perPage)) return page;
                if (first < 0) first = i;
            }
            return first < 0 ? page : PageOf(first, perPage);
        }

        /// <summary>지금 페이지 밖에 표시된 분대가 하나라도 있는가.</summary>
        public static bool AnyOutside(IReadOnlyList<bool> flags, int page, int perPage)
        {
            for (int i = 0; i < flags.Count; i++)
                if (flags[i] && !OnPage(i, page, perPage)) return true;
            return false;
        }

        /// <summary>알릴 만한 분대: 사기가 한계점 이하(낮음 구간).</summary>
        public static bool AtRisk(MoraleBand band) => band == MoraleBand.Low;

        /// <summary>페이지 표시. 괄호가 넘기는 키다. 예: "[  1/2  ]".</summary>
        public static string Label(int page, int pageCount) => $"[  {page + 1}/{Math.Max(1, pageCount)}  ]";
    }
}
