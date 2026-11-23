using System;
using System.Collections.Generic;
using ProjectNeo.Characters;
using ProjectNeo.Combat;
using ProjectNeo.Morale;
using ProjectNeo.Run;
using ProjectNeo.Skills;
using ProjectNeo.Squads;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectNeo.Flow
{
    /// <summary>
    /// 게임 씬의 액티브 스킬 연결(COMBAT-06). 전투 오브젝트에 실행 중에 붙는다(씬 파일을 바꾸지 않는다).
    /// 전투가 시작되면 플레이어와 런의 영웅마다 스킬 칸을 채우고 모두 준비 상태로 만든다.
    /// 전투 중 스킬 키(Z/X/C)는 플레이어가, 보조 키(Alt)와 함께 누르면 선택한 분대의 대장 영웅이 화면 가운데를 겨눠 쓴다.
    /// </summary>
    public sealed class GameSkills : MonoBehaviour
    {
        const float MessageSeconds = 2f;
        GameFlow flow;
        GameBattleSettings settings;
        CharacterActor player;
        SquadCommandCenter squads;
        Func<GameRunLink> runLink;
        Func<Camera> view;
        ISkillContext context;
        InputAction[] cast = new InputAction[0];
        InputAction modifier;
        float messageRemaining;
        bool prepared;
        SkillAutoSettings defaultAuto;

        /// <summary>플레이어 스킬 칸에 넣을 스킬(런 플레이어의 등급으로 열린 고정 스킬). 비어 있으면 플레이어 영웅 데이터의 스킬을 쓴다.</summary>
        public Func<IEnumerable<SkillDefinition>> PlayerSkills { get; set; }
        /// <summary>영웅의 자동 시전 켜짐 여부(런 영웅마다). 비어 있거나 런 영웅이 아니면 켜짐.</summary>
        public Func<RunHero, bool> HeroAuto { get; set; }

        SkillAutoSettings AutoSettings
        {
            get
            {
                if (settings != null && settings.SkillAuto != null) return settings.SkillAuto;
                if (defaultAuto == null) defaultAuto = SkillAutoSettings.Create();
                return defaultAuto;
            }
        }

        public SkillCaster PlayerCaster { get; private set; }
        /// <summary>마지막으로 쓰지 못한 이유. 잠깐 보였다 사라진다.</summary>
        public string Message { get; private set; } = string.Empty;
        public bool BattleActive => flow != null && flow.Phase == GamePhase.Battle;
        public int SlotCount => cast.Length;
        /// <summary>마지막 시전 결과(테스트·HUD용).</summary>
        public SkillCastResult LastResult { get; private set; }

        public string KeyOf(int slot) =>
            slot >= 0 && slot < cast.Length ? cast[slot].GetBindingDisplayString() : string.Empty;
        public string ModifierKey => modifier != null ? modifier.GetBindingDisplayString() : "Alt";

        public void Configure(GameFlow gameFlow, GameBattleSettings config, CharacterActor playerActor,
            SquadCommandCenter squadCenter, GameSquadMorale morale, Func<GameRunLink> link, Func<Camera> camera)
        {
            if (flow != null) flow.PhaseChanged -= OnPhaseChanged;
            flow = gameFlow; settings = config; player = playerActor; squads = squadCenter; runLink = link; view = camera;
            context = new GameSkillContext(squadCenter, morale);
            DisposeInput();
            cast = new InputAction[GameBattleSettings.DefaultCastSkillBindings.Length];
            for (int i = 0; i < cast.Length; i++)
            {
                InputAction source = config != null && config.CastSkill != null && i < config.CastSkill.Count ? config.CastSkill[i] : null;
                // 기존 설정 에셋에는 이 항목이 저장되어 있지 않을 수 있어, 비어 있으면 기본 키를 쓴다.
                cast[i] = source != null && source.bindings.Count > 0 ? source.Clone()
                    : new InputAction("CastSkill" + (i + 1), InputActionType.Button, GameBattleSettings.DefaultCastSkillBindings[i]);
            }
            InputAction sourceModifier = config != null ? config.CaptainCastModifier : null;
            modifier = sourceModifier != null && sourceModifier.bindings.Count > 0 ? sourceModifier.Clone()
                : new InputAction("CaptainCastModifier", InputActionType.Button, GameBattleSettings.DefaultCaptainCastModifierBinding);
            if (isActiveAndEnabled)
            {
                EnableInput(true);
                if (flow != null) flow.PhaseChanged += OnPhaseChanged;
            }
            if (BattleActive) PrepareBattle();
        }

        void OnEnable()
        {
            EnableInput(true);
            if (flow != null) flow.PhaseChanged += OnPhaseChanged;
        }

        void OnDisable()
        {
            EnableInput(false);
            if (flow != null) flow.PhaseChanged -= OnPhaseChanged;
        }

        void OnDestroy()
        {
            DisposeInput();
            if (defaultAuto != null) Destroy(defaultAuto);
        }

        void OnPhaseChanged(GamePhase phase)
        {
            if (phase == GamePhase.Battle) PrepareBattle();
            else { prepared = false; Message = string.Empty; messageRemaining = 0f; }
        }

        /// <summary>
        /// 전투 시작: 플레이어와 영웅 모두 등급으로 열린 고정 스킬로 칸을 채우고 모두 준비 상태로 만든다(플레이어는 늘 최고 등급).
        /// 잡졸은 스킬이 없다.
        /// </summary>
        public void PrepareBattle()
        {
            prepared = true;
            if (player != null)
            {
                PlayerCaster = Attach(player);
                PlayerCaster.Setup(PlayerSkillList());
            }
            GameRunLink link = runLink?.Invoke();
            foreach (CharacterActor actor in HeroActors(link))
            {
                SkillCaster caster = Attach(actor);
                RunHero hero = link?.UnitOf(actor) as RunHero;
                caster.Setup(SkillSlotRules.OpenHeroSkills(actor.Character.Definition.Skills, hero != null ? hero.Grade : 1));
                HeroSkillAI brain = HeroSkillAI.On(caster);
                brain.Configure(AutoSettings);
                brain.Auto = hero == null || HeroAuto == null || HeroAuto(hero);
                brain.Gate = () => BattleActive;
            }
        }

        IEnumerable<SkillDefinition> PlayerSkillList()
        {
            IEnumerable<SkillDefinition> chosen = PlayerSkills?.Invoke();
            if (chosen != null) return chosen;
            return player.Character != null ? player.Character.Skills : null;
        }

        IEnumerable<CharacterActor> HeroActors(GameRunLink link)
        {
            var seen = new HashSet<CharacterActor>();
            if (link != null)
                foreach (CharacterActor actor in link.ActorsInRun())
                    if (IsHero(actor) && seen.Add(actor)) yield return actor;
            if (squads == null) yield break;
            foreach (Squad<UnitCombatAI> squad in squads.Squads)
            {
                CharacterActor captain = squad.Leader != null ? squad.Leader.GetComponent<CharacterActor>() : null;
                if (IsHero(captain) && seen.Add(captain)) yield return captain;
            }
        }

        bool IsHero(CharacterActor actor) => actor != null && actor != player && actor.Character != null;

        SkillCaster Attach(CharacterActor actor)
        {
            SkillCaster caster = SkillCaster.On(actor);
            caster.UseContext(context);
            if (actor.GetComponent<PlaceholderSkillView>() == null) actor.gameObject.AddComponent<PlaceholderSkillView>();
            return caster;
        }

        void Update()
        {
            if (!BattleActive) return;
            if (!prepared) PrepareBattle();
            if (messageRemaining > 0f)
            {
                messageRemaining -= Time.deltaTime;
                if (messageRemaining <= 0f) Message = string.Empty;
            }
            if (!InputAllowed()) return;
            for (int i = 0; i < cast.Length; i++)
            {
                if (!cast[i].WasPressedThisFrame()) continue;
                if (modifier != null && modifier.IsPressed()) CastCaptains(i);
                else CastPlayer(i);
            }
        }

        // 창이 열려 커서가 풀렸거나, 사다리·대포·건설 중이거나, 죽었으면 스킬 키를 받지 않는다.
        bool InputAllowed()
        {
            if (player == null || player.Health == null || player.Health.IsDead) return false;
            if (Cursor.lockState != CursorLockMode.Locked) return false;
            PlayerInputController controller = player.GetComponent<PlayerInputController>();
            return controller == null || !controller.IsMovementSuspended;
        }

        /// <summary>플레이어가 칸의 스킬을 화면 가운데를 겨눠 쓴다.</summary>
        public SkillCastResult CastPlayer(int slot)
        {
            if (PlayerCaster == null) PrepareBattle();
            if (PlayerCaster == null) return SkillCastResult.CannotAct;
            LastResult = PlayerCaster.TryCast(slot, Aim(PlayerCaster.Actor));
            Report(LastResult == SkillCastResult.Cast ? string.Empty : Describe(LastResult, slot));
            return LastResult;
        }

        /// <summary>
        /// 선택한 분대마다 대장 영웅이 칸의 스킬을 화면 가운데를 겨눠 쓴다. 한 명이라도 썼으면 Cast.
        /// Auto 여부와 상관없이 직접 명령은 따른다.
        /// </summary>
        public SkillCastResult CastCaptains(int slot)
        {
            if (squads == null || squads.Commander == null) return Fail(SkillCastResult.NoSkill, "선택한 부대가 없습니다.");
            if (!prepared) PrepareBattle();
            bool anySelected = false, anyCast = false;
            SkillCastResult first = SkillCastResult.NoSkill;
            for (int i = 0; i < squads.Squads.Count; i++)
            {
                if (!squads.Commander.IsSelected(i)) continue;
                anySelected = true;
                UnitCombatAI leader = squads.Squads[i].Leader;
                SkillCaster caster = leader != null ? leader.GetComponent<SkillCaster>() : null;
                if (caster == null) continue;
                SkillCastResult result = caster.TryCast(slot, Aim(caster.Actor));
                if (result == SkillCastResult.Cast) anyCast = true;
                else if (first == SkillCastResult.NoSkill) first = result;
            }
            if (!anySelected) return Fail(SkillCastResult.NoSkill, "먼저 부대를 선택하세요.");
            if (anyCast)
            {
                LastResult = SkillCastResult.Cast;
                Report(string.Empty);
                return LastResult;
            }
            return Fail(first, "대장: " + Describe(first, slot));
        }

        SkillCastResult Fail(SkillCastResult result, string message)
        {
            LastResult = result;
            Report(message);
            return result;
        }

        SkillAim Aim(CharacterActor caster)
        {
            Camera camera = view?.Invoke();
            if (camera == null) return SkillAim.At(caster.transform.position + caster.transform.forward * 5f);
            Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            float distance = settings != null ? settings.SkillAimDistance : 80f;
            float radius = settings != null ? settings.SkillUnitAimRadius : 0.75f;
            return SkillAimFinder.FromRay(ray, distance, radius, caster);
        }

        void Report(string message)
        {
            Message = message;
            messageRemaining = message.Length > 0 ? MessageSeconds : 0f;
        }

        string Describe(SkillCastResult result, int slot)
        {
            switch (result)
            {
                case SkillCastResult.NoSkill: return $"{KeyOf(slot)} 칸에 스킬이 없습니다.";
                case SkillCastResult.NotReady: return "아직 준비되지 않았습니다.";
                case SkillCastResult.CannotAct: return "지금은 스킬을 쓸 수 없습니다.";
                case SkillCastResult.NoTarget: return "대상을 조준하세요.";
                case SkillCastResult.OutOfRange: return "대상이 너무 멉니다.";
                default: return string.Empty;
            }
        }

        void EnableInput(bool enable)
        {
            foreach (InputAction action in AllInput())
            {
                if (enable) action.Enable();
                else action.Disable();
            }
        }

        void DisposeInput()
        {
            foreach (InputAction action in AllInput()) action.Dispose();
            cast = new InputAction[0];
            modifier = null;
        }

        IEnumerable<InputAction> AllInput()
        {
            foreach (InputAction action in cast) if (action != null) yield return action;
            if (modifier != null) yield return modifier;
        }
    }
}
