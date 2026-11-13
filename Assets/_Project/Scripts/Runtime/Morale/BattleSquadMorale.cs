using System;
using ProjectNeo.Core;

namespace ProjectNeo.Morale
{
    /// <summary>
    /// 전투 한 판 동안 부대 하나의 사기 규칙(B-8.1). 일반 C#이며 씬을 모른다.
    /// 사기 값은 런에 이어지는 <see cref="SquadMorale"/>에 두고, 이 객체는 전투가 끝나면 버린다(<see cref="Dispose"/>).
    /// 씬 쪽은 사건(사망·성문 파괴 등)을 알려 주고, 이벤트(한계 판정·도망·근처 부대 사기 변화·자리 이탈)를 받아 처리한다.
    /// "근처"에 누가 있는지는 씬이 <see cref="MoraleSettings.NearbyRadius"/>로 판단한다.
    /// </summary>
    public sealed class BattleSquadMorale : IDisposable
    {
        readonly MoraleSettings settings;
        readonly IRandomSource random;
        float panicTimer;
        float contagionTimer;
        bool evaluating;
        bool disposed;

        public SquadMorale Morale { get; }
        public MoraleSide Side { get; }
        public int StartingMembers { get; }
        public int LostMembers { get; private set; }
        public MoraleState State { get; private set; }
        public CollapseEffect Collapse { get; private set; }
        public AwakenEffect Awaken { get; private set; }

        /// <summary>사기가 한계 판정 기준 미만으로 전투를 시작했다(약한 붕괴 효과, 판정 없음).</summary>
        public bool StartedShaken { get; }

        /// <summary>이번 전투의 한계 판정을 이미 썼다.</summary>
        public bool CheckUsed { get; private set; }

        /// <summary>부대장 특성·퍽이 더하는 각성 확률. 기본 확률에 더한 뒤 0~1로 자른다.</summary>
        public float AwakenChanceBonus { get; set; }

        public bool IsRouted => State == MoraleState.Routed;

        /// <summary>붕괴 효과의 세기. 판정으로 걸리면 1, 100 미만으로 시작해 걸리면 약하다.</summary>
        public float EffectStrength => StartedShaken ? settings.StartingCollapseStrength : 1f;

        /// <summary>처음 인원 중 잃은 비율(0~1).</summary>
        public float LostShare => (float)LostMembers / StartingMembers;

        /// <summary>한계 판정이 일어났다. <see cref="State"/>와 <see cref="Collapse"/>/<see cref="Awaken"/>이 결과다.</summary>
        public event Action<BattleSquadMorale> BreakingPointReached;

        /// <summary>사기 0으로 부대 전체가 도망을 시작했다. 근처 부대에는 씬이 <see cref="OnNearbySquadRouted"/>를 알린다.</summary>
        public event Action<BattleSquadMorale> Routed;

        /// <summary>근처 부대의 사기를 바꿔야 한다(사기 상승 +, 전염 −). 씬이 근처 부대의 <see cref="SquadMorale.Add"/>에 전달한다.</summary>
        public event Action<BattleSquadMorale, float> NearbyMoraleChange;

        /// <summary>겁에 질림: 자리를 버리고 성 안쪽으로 물러날 때가 됐다.</summary>
        public event Action<BattleSquadMorale> PanicRetreat;

        public BattleSquadMorale(SquadMorale morale, MoraleSide side, int startingMembers,
            MoraleSettings settings, IRandomSource random)
        {
            Morale = morale ?? throw new ArgumentNullException(nameof(morale));
            this.settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (startingMembers < 1) throw new ArgumentOutOfRangeException(nameof(startingMembers));

            Side = side;
            StartingMembers = startingMembers;
            State = MoraleState.Normal;

            if (side == MoraleSide.Ally && morale.Value < settings.BreakingPoint)
            {
                StartedShaken = true;
                CheckUsed = true;
                State = MoraleState.Collapsed;
                Collapse = RandomCollapse();
            }

            Morale.Changed += OnMoraleChanged;
        }

        /// <summary>부대원 한 명이 죽었다. 처음 인원 대비 비율만큼 사기가 준다.</summary>
        public void OnMemberDied()
        {
            if (LostMembers < StartingMembers) LostMembers++;
            ApplyLoss(settings.FullSquadLoss / StartingMembers);
        }

        /// <summary>부대장 영웅이 죽었다.</summary>
        public void OnCaptainDied() => ApplyLoss(settings.CaptainDeathLoss);

        /// <summary>메인 성문이 부서졌다. 아군만 영향을 받는다.</summary>
        public void OnGateDestroyed()
        {
            if (Side == MoraleSide.Ally) ApplyLoss(settings.GateDestroyedLoss);
        }

        /// <summary>근처 성벽 조각이 무너졌다. 아군만 영향을 받는다.</summary>
        public void OnNearbyWallCollapsed()
        {
            if (Side == MoraleSide.Ally) ApplyLoss(settings.NearbyWallCollapseLoss);
        }

        /// <summary>근처의 같은 편 부대가 도망을 시작했다.</summary>
        public void OnNearbySquadRouted() => ApplyLoss(settings.NearbySquadRoutLoss);

        /// <summary>공격력 배율. 사기 구간 보정과 결사항전을 합친다.</summary>
        public float AttackMultiplier
        {
            get
            {
                float multiplier = 1f;
                if (Side == MoraleSide.Ally && Morale.Value >= settings.HighBandThreshold)
                    multiplier += settings.HighBandAttackBonus;
                if (Morale.Value < settings.BreakingPoint)
                    multiplier -= settings.LowBandAttackPenalty;
                if (Awaken == AwakenEffect.LastStand)
                    multiplier += settings.LastStandMaxAttackBonus * LostShare;
                return multiplier < 0f ? 0f : multiplier;
            }
        }

        /// <summary>
        /// 새 지시를 받을 때 부른다. 공포에 걸렸으면 확률로 true(지시 무시). 랜덤을 쓰므로 지시마다 한 번만 부른다.
        /// </summary>
        public bool ShouldIgnoreOrder()
        {
            if (IsRouted || State != MoraleState.Collapsed || Collapse != CollapseEffect.Fear) return false;
            return random.NextFloat() < settings.FearIgnoreChance * EffectStrength;
        }

        /// <summary>
        /// 시간 경과. 겁에 질림·전염의 주기 효과를 낸다. 약한 붕괴는 주기가 그만큼 길고 전염 양이 적다.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || State != MoraleState.Collapsed) return;

            float strength = EffectStrength;
            if (strength <= 0f) return;

            if (Collapse == CollapseEffect.Panic)
            {
                float interval = settings.PanicInterval / strength;
                panicTimer += deltaTime;
                while (panicTimer >= interval)
                {
                    panicTimer -= interval;
                    PanicRetreat?.Invoke(this);
                }
            }
            else if (Collapse == CollapseEffect.Contagion)
            {
                float interval = settings.ContagionInterval / strength;
                contagionTimer += deltaTime;
                while (contagionTimer >= interval)
                {
                    contagionTimer -= interval;
                    NearbyMoraleChange?.Invoke(this, -settings.ContagionAmount * strength);
                }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Morale.Changed -= OnMoraleChanged;
        }

        void ApplyLoss(float amount)
        {
            if (disposed || IsRouted || amount <= 0f) return;
            Morale.Add(-amount);
        }

        // 사기는 퍽·스킬이 SquadMorale.Add로 직접 바꿀 수도 있어 값이 바뀔 때마다 판정한다.
        void OnMoraleChanged()
        {
            if (evaluating || disposed || IsRouted) return;
            evaluating = true;
            try
            {
                if (Side == MoraleSide.Ally && !CheckUsed && Morale.Value <= settings.BreakingPoint)
                    RunBreakingPointCheck();

                if (Morale.Value <= 0f && Awaken != AwakenEffect.Steadfast)
                {
                    State = MoraleState.Routed;
                    Routed?.Invoke(this);
                }
            }
            finally
            {
                evaluating = false;
            }
        }

        void RunBreakingPointCheck()
        {
            CheckUsed = true;
            float chance = settings.AwakenChance + AwakenChanceBonus;
            if (chance < 0f) chance = 0f;
            if (chance > 1f) chance = 1f;

            if (random.NextFloat() < chance)
            {
                State = MoraleState.Awakened;
                Awaken = (AwakenEffect)(1 + random.NextInt(3));
            }
            else
            {
                State = MoraleState.Collapsed;
                Collapse = RandomCollapse();
            }

            BreakingPointReached?.Invoke(this);

            if (Awaken == AwakenEffect.Rally)
            {
                Morale.Add(settings.RallyAmount);
                NearbyMoraleChange?.Invoke(this, settings.RallyAmount);
            }
        }

        CollapseEffect RandomCollapse() => (CollapseEffect)(1 + random.NextInt(3));
    }
}
