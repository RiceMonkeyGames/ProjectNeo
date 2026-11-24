using System;
using ProjectNeo.Combat;
using ProjectNeo.Economy;
using ProjectNeo.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 전투 중 플레이어의 소모품 사용(PREP-04). 전투 오브젝트에 실행 중에 붙는다(씬 파일을 바꾸지 않는다).
    /// 사용 키를 누르면 런 보관함의 회복약 하나를 써서 플레이어 체력을 바로 회복한다.
    /// 키와 재사용 대기 시간은 전투 설정(GameBattleSettings), 회복 비율은 아이템 데이터에 있다.
    /// </summary>
    public sealed class GameConsumables : MonoBehaviour
    {
        const float MessageSeconds = 2f;
        GameFlow flow;
        GameBattleSettings settings;
        CharacterActor player;
        Func<RunState> run;
        InputAction use;
        float messageRemaining;

        /// <summary>다시 쓸 수 있을 때까지 남은 시간(초).</summary>
        public float CooldownRemaining { get; private set; }
        /// <summary>마지막 사용 시도의 결과.</summary>
        public ConsumableUseCheck LastCheck { get; private set; }
        /// <summary>마지막으로 쓰지 못한 이유. 잠깐 보였다 사라진다.</summary>
        public string Message { get; private set; } = string.Empty;
        public string UseKey => use != null ? use.GetBindingDisplayString() : "Q";
        /// <summary>소모품을 쓴 순간(아이템, 실제 회복량). 외형(소리·효과)이 듣는다.</summary>
        public event Action<ItemDefinition, float> Used;

        Inventory Stash => run?.Invoke()?.Inventory;
        /// <summary>런 보관함에 남은 쓸 수 있는 소모품 수.</summary>
        public int Count => ConsumableUse.CountUsable(Stash);

        public void Configure(GameFlow gameFlow, GameBattleSettings config, CharacterActor playerActor, Func<RunState> runState)
        {
            flow = gameFlow; settings = config; player = playerActor; run = runState;
            use?.Dispose();
            // 기존 설정 에셋에는 이 항목이 저장되어 있지 않을 수 있어, 비어 있으면 기본 키를 쓴다.
            use = config != null && config.UseConsumable != null && config.UseConsumable.bindings.Count > 0
                ? config.UseConsumable.Clone()
                : new InputAction("UseConsumable", InputActionType.Button, GameBattleSettings.DefaultUseConsumableBinding);
            if (isActiveAndEnabled) use.Enable();
        }

        void OnEnable() => use?.Enable();
        void OnDisable() => use?.Disable();
        void OnDestroy() => use?.Dispose();

        void Update()
        {
            if (flow == null) return;
            if (flow.Phase != GamePhase.Battle)
            {
                // 전투가 끝나면 대기 시간과 안내를 지운다. 다음 전투는 바로 쓸 수 있는 상태로 시작한다.
                CooldownRemaining = 0f; messageRemaining = 0f; Message = string.Empty;
                return;
            }
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            if (messageRemaining > 0f)
            {
                messageRemaining -= Time.deltaTime;
                if (messageRemaining <= 0f) Message = string.Empty;
            }
            if (use != null && use.WasPressedThisFrame()) TryUse();
        }

        /// <summary>소모품 하나를 쓴다. 전투 중이고, 살아 있고, 다쳤고, 대기 시간이 끝났을 때만 쓴다.</summary>
        public ConsumableUseCheck TryUse()
        {
            LastCheck = Evaluate(out ItemInstance item);
            if (LastCheck != ConsumableUseCheck.Ok)
            {
                Message = Describe(LastCheck);
                messageRemaining = MessageSeconds;
                return LastCheck;
            }
            Health health = player.Health;
            float amount = ConsumableUse.HealAmount(item.Definition, health.Maximum);
            Stash.Remove(item);
            float healed = health.Heal(amount);
            CooldownRemaining = settings != null ? settings.ConsumableCooldown : 0f;
            Message = string.Empty; messageRemaining = 0f;
            Used?.Invoke(item.Definition, healed);
            return LastCheck;
        }

        ConsumableUseCheck Evaluate(out ItemInstance item)
        {
            item = null;
            if (flow == null || flow.Phase != GamePhase.Battle) return ConsumableUseCheck.NotInBattle;
            Health health = player != null ? player.Health : null;
            if (health == null) return ConsumableUseCheck.Dead;
            item = ConsumableUse.FindUsable(Stash);
            return ConsumableUse.Check(item, !health.IsDead, health.Current, health.Maximum, CooldownRemaining);
        }

        static string Describe(ConsumableUseCheck check)
        {
            switch (check)
            {
                case ConsumableUseCheck.NoItem: return "사용할 소모품이 없습니다.";
                case ConsumableUseCheck.FullHealth: return "체력이 가득 찼습니다.";
                case ConsumableUseCheck.CoolingDown: return "아직 준비되지 않았습니다.";
                default: return string.Empty;
            }
        }

        /// <summary>전투 화면 안내 한 줄. 소모품이 있으면 키·이름·개수와 남은 대기 시간, 방금 못 썼으면 이유를 보여준다.</summary>
        /// <summary>전투 키 가이드 소모품 줄의 하는 일(UI-13): "회복 물약 ×2 (3초)", 없으면 "소모품 없음".</summary>
        public string GuideText()
        {
            int count = Count;
            string name = count > 0 ? ConsumableUse.FindUsable(Stash).Definition.DisplayName : string.Empty;
            return ProjectNeo.UI.BattleHudText.Consumable(name, count, CooldownRemaining);
        }

        public string Hint()
        {
            int count = Count;
            string text = string.Empty;
            if (count > 0)
            {
                ItemInstance item = ConsumableUse.FindUsable(Stash);
                string name = item.Definition.DisplayName;
                text = $"{UseKey} · {name} ×{count}";
                if (CooldownRemaining > 0f)
                    text += $" ({Mathf.CeilToInt(CooldownRemaining)}초 후 사용 가능)";
            }
            if (Message.Length > 0) text += (text.Length > 0 ? " · " : string.Empty) + Message;
            return text;
        }
    }
}
