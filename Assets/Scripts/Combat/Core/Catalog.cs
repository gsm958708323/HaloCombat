using System;

namespace Combat.Core
{
    public static class CombatCatalog
    {
        public static DurationSpec Burn()
        {
            return new DurationSpec
            {
                BuffId = CombatIds.Burn,
                Duration = 3f,
                TickInterval = 1f,
                MaxStacks = 3,
                Stack = StackPolicy.AddStack,
                OnPeriod = new IEffect[]
                {
                    new DamageEffect
                    {
                        Coeff = 0.2f,
                        CanCrit = false,
                        UseSnapshotAtk = true,
                        ScaleByBuffStacks = true
                    }
                }
            };
        }

        public static ProjectileDefinition Fireball()
        {
            return new ProjectileDefinition
            {
                SpecId = CombatIds.Fireball,
                Speed = 14f,
                Lifetime = 2f,
                HitRadius = 0.3f,
                MaxHits = 1,
                SnapshotAtk = true,
                SpawnForward = 0.4f
            };
        }

        public static AoeDefinition FireGround()
        {
            return new AoeDefinition
            {
                SpecId = CombatIds.FireGround,
                Radius = 1.3f,
                Duration = 2f,
                PulseInterval = 0.45f,
                PulseOnSpawn = true,
                TrackOccupancy = false,
                CueId = CombatIds.CueFireGround
            };
        }

        public static void RegisterDefaults(ProjectileCatalog proj, AoeCatalog aoe, DurationSpec burn)
        {
            var fb = Fireball();
            fb.OnHit = new IEffect[]
            {
                new DamageEffect { Coeff = 1f, CanCrit = true, UseSnapshotAtk = true },
                new ApplyDurationEffect(burn),
                new PlayCueEffect(CombatIds.CueFireballHit, "FireballHit")
            };
            proj.Register(fb);

            var ground = FireGround();
            ground.OnPulse = new IEffect[] { new ApplyDurationEffect(burn) };
            aoe.Register(ground);
        }
    }

    public sealed class HitProfileBake
    {
        public DamageEffect Damage = new DamageEffect { Coeff = 1f, CanCrit = true, UseSnapshotAtk = true };
        public HitStunEffect Stun = new HitStunEffect { Duration = 0.35f };
        public KnockbackEffect Knockback = new KnockbackEffect { Distance = 0.4f };
        public IFrameEffect IFrame;
        IEffect[] _baked;

        public IEffect[] Bake()
        {
            if (_baked != null) return _baked;
            int n = 2;
            if (Knockback != null) n++;
            if (IFrame != null) n++;
            var bag = new IEffect[n];
            int i = 0;
            bag[i++] = Damage;
            bag[i++] = Stun;
            if (Knockback != null) bag[i++] = Knockback;
            if (IFrame != null) bag[i++] = IFrame;
            _baked = bag;
            return _baked;
        }

        public void ClearCache() => _baked = null;
    }

    public sealed class DurationBake
    {
        public DurationSpec Source;
        IEffect[] _period;
        public DurationBake(DurationSpec source) => Source = source;

        public DurationSpec Bake()
        {
            if (_period == null) _period = Source.OnPeriod ?? Array.Empty<IEffect>();
            Source.OnPeriod = _period;
            return Source;
        }

        public void ClearCache() => _period = null;
    }
}
