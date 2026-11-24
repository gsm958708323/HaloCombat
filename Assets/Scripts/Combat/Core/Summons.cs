using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class SummonDefinition
    {
        public int SpecId;
        public string BlueprintId = "summon", OriginContentKey;
        public int Count = 1, MaxAlive = 64;
        public float Spacing = 1, CollisionRadius = .4f;
        public float Lifetime;
        public float FollowRange = 2f;
        public float AcquireRadius = 8f;
        public BtNode Tree;
        public IEffect[] OnSpawn = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
        public IEffect[] OnRemoved = Array.Empty<IEffect>();
    }

    public sealed class SummonCatalog
    {
        readonly Dictionary<int, SummonDefinition> _map = new Dictionary<int, SummonDefinition>();
        public IEnumerable<SummonDefinition> All => _map.Values;
        public void Register(SummonDefinition def)
        {
            if (def == null || def.SpecId == 0) throw new ArgumentException("SummonDefinition");
            _map[def.SpecId] = def;
        }
        public bool TryGet(int id, out SummonDefinition def) => _map.TryGetValue(id, out def);
    }

    public sealed class SummonComp : Comp
    {
        public EntityId OwnerId { get; private set; }
        public float Lifetime { get; private set; }
        public double Age { get; private set; }
        public float Power { get; private set; } = 1;
        public float SnapshotAttack { get; private set; }
        public bool Ending { get; private set; }
        public bool CleanupWithOwner => true;
        public SummonDefinition Def { get; private set; }
        public SkillOrigin Origin {get;private set;}
        public override bool WantsTick => true;
        public void Setup(EntityId owner, SummonDefinition def, float snapshotAttack = 0, float power = 1,SkillOrigin origin=default)
        {
            OwnerId = owner; Def = def; Lifetime = def != null ? def.Lifetime : 0; Age = 0;
            Power = power; SnapshotAttack = snapshotAttack; Ending = false;
            Origin=origin;
            if (Self.TryGetComp<SkillCastComp>(out var cast)) cast.SetBasePower(power);
            if (Self.TryGetComp<BehaviorTreeComp>(out var bt))
            {
                bt.Board.Owner = owner;
                if (def != null) { bt.Board.AcquireRadius = def.AcquireRadius; bt.Board.FollowRange = def.FollowRange; }
                if (def?.Tree != null) bt.SetTree(def.Tree);
            }
        }
        public override void Tick(float dt)
        {
            if (Ending || Def == null || Self.World == null) return;
            if (Self.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead)) { End(SummonEventKind.Killed); return; }
            if (!Self.World.TryGetActor(OwnerId, out var owner) || (owner.TryGetComp<TagComp>(out var ownerTags) && ownerTags.Has(CommonTags.Dead)))
            { End(SummonEventKind.OwnerRemoved); return; }
            Age += dt;
            if (Lifetime > 0 && Age + .000001f >= Lifetime) End(SummonEventKind.Expired);
        }
        public void End(SummonEventKind reason)
        {
            if (Ending || Def == null || Self.World == null) return;
            Ending = true;
            var world = Self.World;
            if (Self.TryGetComp<SkillDirectorComp>(out var director)) director.Stop(DirectorStopReason.Manual);
            world.CleanupByOwner(Self.Id);
            world.TryGetActor(OwnerId, out var owner);
            var point = Self.GetComp<TransformComp>().Position;
            if (reason == SummonEventKind.Expired) world.Deliver(Def.OnExpire, owner, Self, SnapshotAttack, point, power: Power,origin:Origin);
            world.Deliver(Def.OnRemoved, owner, Self, SnapshotAttack, point, power: Power,origin:Origin);
            world.Events.Publish(new EvSummonLifecycle(Self.Id, OwnerId, Def.SpecId, reason));
            world.RequestDespawn(Self.Id); Self.SetActive(false);
        }
        protected override void OnDetach()
        { Def = null; OwnerId = EntityId.Invalid; Age = 0; Lifetime = SnapshotAttack = 0; Power = 1; Ending = false; }
    }

    public sealed class SpawnSummonEffect : IEffect
    {
        readonly int _specId;
        readonly EffectCenter _center;
        public SpawnSummonEffect(int specId, EffectCenter center = EffectCenter.Auto) { _specId = specId; _center = center; }
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null || ctx.Source == null || !ctx.Source.IsActive || ctx.Source.World != ctx.World) return;
            if (ctx.Source.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead)) return;
            if (ctx.Source.TryGetComp<SummonComp>(out var sourceBody) && sourceBody.Ending) return;
            if (!ctx.World.Summons.TryGet(_specId, out var def)) throw new InvalidOperationException("Unknown summon " + _specId);
            if (!EffectPosition.TryResolve(ctx, _center, out var origin)) return;
            var transform = ctx.Source.GetComp<TransformComp>();
            float yaw = ctx.HasDir ? LocomotionComp.YawFromStick(ctx.Dir) : transform.YawDegrees;
            if (_center == EffectCenter.Auto && !ctx.HasPoint)
            { var direction = LocomotionComp.ForwardFromYaw(yaw); origin = new SimVec3(origin.X + direction.X * .8f, origin.Y, origin.Z + direction.Z * .8f); }
            for (int i = 0; i < def.Count; i++)
            {
                int total = 0, owned = 0;
                foreach (var actor in ctx.World.RegistryActive())
                    if (actor.TryGetComp<SummonComp>(out var body) && !body.Ending)
                    { total++; if (body.OwnerId == ctx.Source.Id && body.Def?.SpecId == def.SpecId) owned++; }
                if (total >= 128 || owned >= def.MaxAlive)
                { Reject(ctx, total >= 128 ? "WorldLimit" : "OwnerLimit"); break; }
                var position = origin;
                if (def.Count > 1)
                { var direction = LocomotionComp.ForwardFromYaw(yaw + i * 360f / def.Count); position = new SimVec3(origin.X + direction.X * def.Spacing, origin.Y, origin.Z + direction.Z * def.Spacing); }
                if (!ctx.World.Collision.IsFree(position, def.CollisionRadius)) { Reject(ctx, "BlockedPosition"); continue; }
                var id = ctx.World.SpawnActor(new ActorSpawnSpec(def.BlueprintId), false);
                if (!ctx.World.TryGetActor(id, out var pet)) continue;
                pet.GetComp<TransformComp>().Position = position; pet.GetComp<TransformComp>().YawDegrees = yaw;
                SpawnAoeEffect.CopyTeam(ctx.Source, pet);
                pet.GetComp<SummonComp>().Setup(ctx.Source.Id, def, ctx.SnapshotAtk, ctx.Power > 0 ? ctx.Power : 1,ctx.Origin);
                ctx.World.PublishSpawn(id, "summon");
                ctx.World.Events.Publish(new EvSummonLifecycle(id, ctx.Source.Id, def.SpecId, SummonEventKind.Spawned, def.BlueprintId));
                ctx.World.Deliver(def.OnSpawn, ctx.Source, pet, ctx.SnapshotAtk, position, power: ctx.Power,origin:ctx.Origin);
            }
        }
        void Reject(in EffectContext ctx, string reason)
            => ctx.World.Events.Publish(new EvSummonLifecycle(EntityId.Invalid, ctx.Source.Id, _specId, SummonEventKind.Rejected, reason));
    }
}
