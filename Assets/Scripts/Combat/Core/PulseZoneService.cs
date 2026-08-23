using System;

namespace Combat.Core
{
    public sealed class PulseZoneService
    {
        readonly CombatWorld _world;
        readonly IntentQueue _intents;
        readonly PulseZoneSpecLibrary _specs;
        readonly AoESpecLibrary _aoeSpecs;
        readonly CombatActorFactory _factory;

        public PulseZoneService(
            CombatWorld world,
            IntentQueue intents,
            PulseZoneSpecLibrary specs,
            AoESpecLibrary aoeSpecs,
            CombatActorFactory factory)
        {
            _world = world;
            _intents = intents;
            _specs = specs;
            _aoeSpecs = aoeSpecs;
            _factory = factory;
        }

        public void Tick()
        {
            _intents.Drain<SpawnPulseZoneIntent>(SpawnOne);
        }

        void SpawnOne(SpawnPulseZoneIntent intent)
        {
            if (!_specs.TryGet(intent.SpecValue, out var spec))
                return;
            if (!_world.TryGetActor(intent.Owner, out var owner))
                return;

            var otf = owner.GetComp<TransformComp>();
            int team = 0;
            if (owner.TryGetComp<TeamComp>(out var t))
                team = t.Team;

            int skill = 0;
            if (owner.TryGetComp<SkillDirectorComp>(out var dir))
                skill = dir.CurrentSkill.Value;

            float radius = spec.Radius;
            int atk = spec.AttackSpecValue;
            if (spec.AoESpecValue != 0 && _aoeSpecs.TryGet(spec.AoESpecValue, out var aoe))
            {
                radius = aoe.Radius;
                atk = aoe.AttackSpecValue;
            }

            var offset = otf.LocalToWorld(spec.OffsetX, spec.OffsetY, spec.OffsetZ);
            var pos = otf.Position + offset;

            var ctx = new PulseZoneSpawnContext
            {
                IsValid = true,
                Owner = intent.Owner,
                OwnerTeam = team,
                Position = pos,
                Radius = radius,
                Interval = spec.Interval,
                Lifetime = spec.Lifetime,
                AttackSpecValue = atk,
                SourceSkillValue = skill
            };

            _factory.SetPendingPulseZone(ctx);
            var id = _world.SpawnActor(new ActorSpawnSpec("pulse_zone"));
            if (_world.TryGetActor(id, out var zone))
                PulseZoneSetup.Apply(zone, ctx);
        }
    }

    public static class PulseZoneSetup
    {
        public static void Apply(Actor zone, in PulseZoneSpawnContext ctx)
        {
            zone.GetComp<TransformComp>().Teleport(ctx.Position);
            zone.GetComp<PulseZoneComp>().Setup(
                ctx.Owner,
                ctx.OwnerTeam,
                ctx.Radius,
                ctx.Interval,
                ctx.Lifetime,
                ctx.AttackSpecValue,
                ctx.SourceSkillValue);
        }
    }
}
