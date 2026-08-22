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
        bool _invulnerable;

        public float Hp => _hp;
        public float MaxHp => _attr != null ? _attr.BaseMaxHp : 0f;
        public bool IsDead => _hp <= 0f;
        public bool Invulnerable
        {
            get => _invulnerable;
            set => _invulnerable = value;
        }

        protected override void OnAttach()
        {
            _attr = Self.GetComp<AttrComp>();
            _hp = _attr.BaseMaxHp;
        }

        protected override void OnDetach()
        {
            _attr = null;
        }

        public void ResetToFull()
        {
            if (_attr != null)
                _hp = _attr.BaseMaxHp;
        }

        /// <summary>仅 DamageService 应调用。</summary>
        public DamageApplyResult ApplyDamage(in DamageApplyArgs args)
        {
            if (IsDead)
                return new DamageApplyResult(0f, _hp, true, false);

            if (_invulnerable || args.Amount <= 0f)
                return new DamageApplyResult(0f, _hp, false, false);

            _hp -= args.Amount;
            if (_hp < 0f)
                _hp = 0f;

            bool died = _hp <= 0f;
            bool stun = !died && args.ApplyHitStun && args.Amount > 0f;
            return new DamageApplyResult(args.Amount, _hp, died, stun);
        }
    }
}
