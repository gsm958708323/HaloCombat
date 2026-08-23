using System;

namespace Combat.Core
{
    /// <summary>阵营。发射者、木桩、投射物主人都可靠它，不必人人有 Hurtbox。</summary>
    public sealed class TeamComp : Comp
    {
        public int Team { get; private set; }

        public void SetTeam(int team) => Team = team;
    }

    public readonly struct DamageApplyArgs
    {
        public readonly EntityId Source;
        public readonly EntityId Owner;
        public readonly float Amount;
        public readonly int AttackSpecValue;
        public readonly bool ApplyHitStun;
        public readonly float HitDuration;

        public DamageApplyArgs(
            EntityId source,
            EntityId owner,
            float amount,
            int attackSpecValue,
            bool applyHitStun,
            float hitDuration)
        {
            Source = source;
            Owner = owner;
            Amount = amount;
            AttackSpecValue = attackSpecValue;
            ApplyHitStun = applyHitStun;
            HitDuration = hitDuration;
        }
    }

    public readonly struct DamageApplyResult
    {
        public readonly float FinalDamage;
        public readonly float HpAfter;
        public readonly bool Died;
        public readonly bool AppliedHitStun;

        public DamageApplyResult(float finalDamage, float hpAfter, bool died, bool appliedHitStun)
        {
            FinalDamage = finalDamage;
            HpAfter = hpAfter;
            Died = died;
            AppliedHitStun = appliedHitStun;
        }
    }

   public sealed class HealthComp : Comp
    {
        AttrComp _attr;
        float _hp;
        float _iframeLeft;
        bool _debugInvulnerable;
        public float Hp => _hp;
        public float MaxHp => _attr != null ? _attr.TotalMaxHp : (_attr?.BaseMaxHp ?? 0f);
        public bool IsDead => _hp <= 0f;
        public bool InIFrame => _iframeLeft > 0f;
        /// <summary>GM/技能霸体等；与 i-frame 叠加为「不可受伤」。</summary>
        public bool Invulnerable
        {
            get => _debugInvulnerable || _iframeLeft > 0f;
            set => _debugInvulnerable = value;
        }
        public override bool WantsTick => true;
        protected override void OnAttach()
        {
            _attr = Self.GetComp<AttrComp>();
            _hp = MaxHp;
            _iframeLeft = 0f;
        }
        public void ResetToFull()
        {
            _hp = MaxHp;
            _iframeLeft = 0f;
        }
        public void BeginIFrame(float seconds)
        {
            if (seconds <= 0f) return;
            if (seconds > _iframeLeft)
                _iframeLeft = seconds;
        }
        public override void Tick(float dt)
        {
            if (_iframeLeft > 0f)
            {
                _iframeLeft -= dt;
                if (_iframeLeft < 0f) _iframeLeft = 0f;
            }
        }
        public DamageApplyResult ApplyDamage(in DamageApplyArgs args)
        {
            if (IsDead)
                return new DamageApplyResult(0f, _hp, true, false);
            if (Invulnerable || args.Amount <= 0f)
                return new DamageApplyResult(0f, _hp, false, false);
            _hp -= args.Amount;
            if (_hp < 0f) _hp = 0f;
            bool died = _hp <= 0f;
            bool stun = !died && args.ApplyHitStun;
            return new DamageApplyResult(args.Amount, _hp, died, stun);
        }
    }
}
