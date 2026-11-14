using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public static class AoePulse
    {
        public static void PulseNow(CombatWorld world, Actor aoe, AoeComp body)
        {
            if (world == null || aoe == null || body?.Def == null || body.Ending || !aoe.IsActive) return;
            world.TryGetActor(body.OwnerId, out var owner);
            var point = aoe.GetComp<TransformComp>().Position;
            var targets = AreaTargets.Query(world, point, body.Radius, owner, body.Def.HostileMask, body.Def.MaxTargets);
            world.Events.Publish(new EvAoeLifecycle(aoe.Id, body.OwnerId, EntityId.Invalid, AoeEventKind.Pulse));
            foreach (var id in targets)
            {
                if (body.Ending || !aoe.IsActive) break;
                if (AreaTargets.TryLive(world, id, out var target))
                    DeliverBag(world, body.Def.OnPulse, owner, target, body.SnapshotAtk, point, body.Power,body.Origin);
            }
        }

        public static void DeliverBag(CombatWorld world, IEffect[] bag, Actor source, Actor target, float snapshotAtk, SimVec3 point, float power = 1,SkillOrigin origin=default)
        { if (bag != null && bag.Length > 0) world.Deliver(bag, source, target, snapshotAtk, point, power: power,origin:origin); }
    }

    public sealed class AoeService
    {
        readonly CombatWorld _world;
        public AoeService(CombatWorld world) => _world = world ?? throw new ArgumentNullException(nameof(world));

        public void Tick(float dt)
        {
            foreach (var aoe in _world.RegistryActive())
            {
                if (!aoe.IsActive || _world.IsActorStopped(aoe) || !aoe.TryGetComp<AoeComp>(out var body) || body.Def == null || body.Ending) continue;
                if (body.BornFrame == _world.Time.Frame) continue;
                var def = body.Def;
                double previousAge=body.Age;double nextAge = body.Age + dt;
                body.Age = def.Duration > 0 ? Math.Min(nextAge, def.Duration) : nextAge;
                _world.TryGetActor(body.OwnerId, out var owner);
                if(!AoeInteractions.Move(_world,aoe,body,(float)(body.Age-previousAge))){DespawnAoe(aoe,body,owner);continue;}
                AoeInteractions.Absorb(_world,aoe,body,owner);
                if(body.Ending||!aoe.IsActive)continue;
                var point = aoe.GetComp<TransformComp>().Position;
                var targets = AreaTargets.Query(_world, point, body.Radius, owner, def.HostileMask, def.MaxTargets);
                if (def.TrackOccupancy) DiffOccupancy(aoe, body, targets, point);
                if (body.Ending || !aoe.IsActive) continue;

                if (def.PulseInterval > 0)
                    while (!body.Ending && aoe.IsActive)
                    {
                        double time = body.NextPulse * (double)def.PulseInterval;
                        if (time > body.Age + .000001 || (def.Duration > 0 && time >= def.Duration - .000001)) break;
                        body.NextPulse++;
                        AoePulse.PulseNow(_world, aoe, body);
                    }
                if (body.Ending || !aoe.IsActive) continue;

                if (def.TrackOccupancy && def.OnStay != null && def.OnStay.Length > 0)
                    foreach (var id in targets)
                    {
                        if (body.Ending) break;
                        if (AreaTargets.TryLive(_world, id, out var target)) DeliverOccupancy(aoe, body, target, point, AoeEventKind.Stay, def.OnStay);
                    }
                if (!body.Ending && def.Duration > 0 && nextAge + .000001 >= def.Duration)
                    DespawnAoe(aoe, body, owner, true);
            }
        }

        void DiffOccupancy(Actor aoe, AoeComp body, EntityId[] targets, SimVec3 point)
        {
            var inside = body.Inside;
            if (inside == null) return;
            var now = new HashSet<long>();
            foreach (var id in targets) now.Add(HitboxComp.Pack(id));
            var previous = new List<long>(inside);
            foreach (var packed in previous)
            {
                if (body.Ending) return;
                if (now.Contains(packed)) continue;
                inside.Remove(packed);
                if (_world.TryGetActor(HitboxComp.Unpack(packed), out var target))
                    DeliverOccupancy(aoe, body, target, point, AoeEventKind.Exit, body.Def.OnExit);
            }
            foreach (var id in targets)
            {
                if (body.Ending) return;
                if (!AreaTargets.TryLive(_world, id, out var target) || !inside.Add(HitboxComp.Pack(id))) continue;
                DeliverOccupancy(aoe, body, target, point, AoeEventKind.Enter, body.Def.OnEnter);
            }
        }

        void DeliverOccupancy(Actor aoe, AoeComp body, Actor target, SimVec3 point, AoeEventKind kind, IEffect[] effects)
        {
            _world.Events.Publish(new EvAoeLifecycle(aoe.Id, aoe.Id, target.Id, kind));
            AoePulse.DeliverBag(_world, effects, aoe, target, body.SnapshotAtk, point, body.Power,body.Origin);
        }

        public void DespawnAoe(Actor aoe, AoeComp body, Actor owner, bool expired = false)
        {
            if (body.Ending || body.Def == null) return;
            body.Ending = true;
            var point = aoe.GetComp<TransformComp>().Position;
            if (body.Inside != null)
            {
                var occupied = new List<long>(body.Inside); body.Inside.Clear();
                foreach (var packed in occupied)
                    if (_world.TryGetActor(HitboxComp.Unpack(packed), out var target))
                        DeliverOccupancy(aoe, body, target, point, AoeEventKind.Exit, body.Def.OnExit);
            }
            _world.Events.Publish(new EvAoeLifecycle(aoe.Id, body.OwnerId, EntityId.Invalid, expired ? AoeEventKind.Expired : AoeEventKind.Removed));
            if (expired) AoePulse.DeliverBag(_world, body.Def.OnExpire, owner, null, body.SnapshotAtk, point, body.Power,body.Origin);
            _world.RequestDespawn(aoe.Id); aoe.SetActive(false);
        }
    }
}
