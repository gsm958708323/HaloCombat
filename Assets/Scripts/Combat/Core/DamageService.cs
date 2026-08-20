using System;

namespace Combat.Core
{
    public readonly struct DamageAppliedEvent
    {
        public readonly EntityId Source;
        public readonly EntityId Target;
        public readonly EntityId Owner;
        public readonly float Amount;
        public readonly float HpAfter;
        public readonly bool Died;
        public readonly bool EnteredHit;

        public DamageAppliedEvent(
            EntityId source,
            EntityId target,
            EntityId owner,
            float amount,
            float hpAfter,
            bool died,
            bool enteredHit)
        {
            Source = source;
            Target = target;
            Owner = owner;
            Amount = amount;
            HpAfter = hpAfter;
            Died = died;
            EnteredHit = enteredHit;
        }
    }

    public sealed class DamageService
    {
        readonly CombatWorld _world;
        readonly IntentQueue _intents;
        readonly AttackSpecLibrary _attacks;
        readonly EventBus _events;

        public DamageService(
            CombatWorld world,
            IntentQueue intents,
            AttackSpecLibrary attacks,
            EventBus events)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
            _attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            _events = events; // 可空：无表现也不影响逻辑
        }

        public void Tick()
        {
            _intents.Drain<HitIntent>(ResolveHit);
        }

        void ResolveHit(HitIntent hit)
        {
            if (!_world.TryGetActor(hit.Target, out var target))
                return;

            // 目标已死：不再结算
            if (!target.TryGetComp<HealthComp>(out var health) || health.IsDead)
                return;

            // 攻击者属性：优先 Owner（投射物归属），否则 Source
            AttrComp atkAttr = null;
            EntityId atkActorId = hit.Owner.IsValid ? hit.Owner : hit.Source;
            if (_world.TryGetActor(atkActorId, out var attacker))
                attacker.TryGetComp(out atkAttr);

            target.TryGetComp(out AttrComp defAttr);

            if (!_attacks.TryGet(hit.AttackSpecValue, out var spec))
            {
                // 未知规格：保底
                spec = new AttackSpec { Id = hit.AttackSpecValue, Power = 1f, ApplyHitStun = true, StunDuration = 0.35f };
            }

            float amount = DamageFormula.Compute(atkAttr, defAttr, spec);

            var applyArgs = new DamageApplyArgs(
                hit.Source,
                hit.Owner,
                amount,
                hit.AttackSpecValue,
                spec.ApplyHitStun,
                spec.StunDuration);

            var result = health.ApplyDamage(applyArgs);

            bool enteredHit = false;
            if (target.TryGetComp<StateMachineComp>(out var fsm))
            {
                if (result.Died)
                {
                    fsm.TryEnter(ActorStateId.Dead, new StateEnterArgs(fsm.Current, "LethalDamage"));
                }
                else if (result.AppliedHitStun)
                {
                    fsm.SetHitDuration(spec.StunDuration);
                    enteredHit = fsm.TryEnter(ActorStateId.Hit, new StateEnterArgs(fsm.Current, "Damage"));
                }
            }

            _events?.Publish(new DamageAppliedEvent(
                hit.Source,
                hit.Target,
                hit.Owner,
                result.FinalDamage,
                result.HpAfter,
                result.Died,
                enteredHit));
        }
    }
}
