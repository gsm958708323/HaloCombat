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
            if (!_world.TryGetActor(hit.Target, out var target)) return;
            if (!target.TryGetComp<HealthComp>(out var health) || health.IsDead) return;

            // i-frame / 无敌：直接吞掉（可选择不发事件）
            if (health.Invulnerable) return;

            AttrComp atkAttr = null;
            EntityId atkId = hit.Owner.IsValid ? hit.Owner : hit.Source;
            Actor attacker = null;
            if (_world.TryGetActor(atkId, out attacker))
                attacker.TryGetComp(out atkAttr);

            target.TryGetComp(out AttrComp defAttr);

            if (!_attacks.TryGet(hit.AttackSpecValue, out var spec))
                spec = new AttackSpec { Id = hit.AttackSpecValue, Power = 1f, StunDuration = 0.35f };

            float amount = DamageFormula.Compute(atkAttr, defAttr, spec);
            var result = health.ApplyDamage(new DamageApplyArgs(
                hit.Source, hit.Owner, amount, hit.AttackSpecValue,
                spec.ApplyHitStun, spec.StunDuration));

            if (result.FinalDamage <= 0f && !result.Died)
                return;

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
                    // 已在 Hit 时 TryEnter 会刷新 timer

                    if (spec.HitIFrame > 0f)
                        health.BeginIFrame(spec.HitIFrame);

                    ApplyKnockback(attacker, target, spec);
                }
            }

            _events?.Publish(new DamageAppliedEvent(
                hit.Source, hit.Target, hit.Owner,
                result.FinalDamage, result.HpAfter, result.Died, enteredHit));
        }

        static void ApplyKnockback(Actor attacker, Actor target, AttackSpec spec)
        {
            if (spec.KnockbackDistance <= 0f) return;
            if (!target.TryGetComp<LocomotionComp>(out var loco)) return;
            if (!target.TryGetComp<TransformComp>(out var ttf)) return;

            float dx, dz;
            if (spec.KnockbackUseAttackerFacing && attacker != null &&
                attacker.TryGetComp<TransformComp>(out var atf))
            {
                var f = atf.ForwardXZ();
                dx = f.X; dz = f.Z;
            }
            else if (attacker != null && attacker.TryGetComp<TransformComp>(out var a2))
            {
                dx = ttf.Position.X - a2.Position.X;
                dz = ttf.Position.Z - a2.Position.Z;
            }
            else
            {
                dx = 1f; dz = 0f;
            }

            float lenSq = dx * dx + dz * dz;
            if (lenSq < 1e-8f) { dx = 1f; dz = 0f; lenSq = 1f; }
            float inv = 1f / MathF.Sqrt(lenSq);
            loco.AddHitDelta(dx * inv * spec.KnockbackDistance, 0f, dz * inv * spec.KnockbackDistance);
        }
    }
}
