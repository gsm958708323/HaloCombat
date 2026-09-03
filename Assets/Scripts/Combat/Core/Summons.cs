using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class SummonDefinition
    {
        public int SpecId;
        public float Lifetime;
        public float FollowRange = 2f;
        public float AcquireRadius = 8f;
        public BtNode Tree;
        public IEffect[] OnSpawn = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
    }

    public sealed class SummonCatalog
    {
        readonly Dictionary<int, SummonDefinition> _map = new Dictionary<int, SummonDefinition>(4);
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
        public float Age { get; private set; }
        public bool CleanupWithOwner { get; private set; } = true;
        public SummonDefinition Def { get; private set; }
        public override bool WantsTick => true;
        public void Setup(EntityId owner, SummonDefinition def)
        {
            OwnerId = owner; Def = def; Lifetime = def != null ? def.Lifetime : 0f; Age = 0f;
            if (Self.TryGetComp<BehaviorTreeComp>(out var bt))
            {
                bt.Board.Owner = owner;
                if (def != null) { bt.Board.AcquireRadius = def.AcquireRadius; bt.Board.FollowRange = def.FollowRange; }
                if (def != null && def.Tree != null) bt.SetTree(def.Tree);
            }
        }
        public override void Tick(float dt)
        {
            if (Lifetime <= 0f || Self.World == null) return;
            Age += dt;
            if (Age < Lifetime) return;
            if (Def != null && Def.OnExpire != null && Def.OnExpire.Length > 0)
            {
                Self.World.TryGetActor(OwnerId, out var owner);
                Self.World.Deliver(Def.OnExpire, owner, Self, 0f);
            }
            Self.World.CleanupByOwner(Self.Id);
            Self.World.RequestDespawn(Self.Id); Self.SetActive(false);
        }
        protected override void OnDetach() { Def = null; OwnerId = EntityId.Invalid; Age = 0f; Lifetime = 0f; }
    }

    public sealed class SpawnSummonEffect : IEffect
    {
        readonly int _specId;
        public SpawnSummonEffect(int specId) => _specId = specId;
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null || ctx.Source == null) return;
            if (!ctx.World.Summons.TryGet(_specId, out var def)) throw new InvalidOperationException("Unknown summon " + _specId);
            var id = ctx.World.SpawnActor(new ActorSpawnSpec("summon"), publishSpawn: false);
            if (!ctx.World.TryGetActor(id, out var pet) || pet == null) return;
            SimVec3 origin;
            if (ctx.HasPoint) origin = ctx.Point;
            else if (ctx.Source.TryGetComp<TransformComp>(out var stf))
            {
                var f = LocomotionComp.ForwardFromYaw(stf.YawDegrees);
                origin = new SimVec3(stf.Position.X + f.X * 0.8f, stf.Position.Y, stf.Position.Z + f.Z * 0.8f);
            }
            else origin = SimVec3.Zero;
            pet.GetComp<TransformComp>().Position = origin;
            if (ctx.Source.TryGetComp<TransformComp>(out var yaw)) pet.GetComp<TransformComp>().YawDegrees = yaw.YawDegrees;
            SpawnAoeEffect.CopyTeam(ctx.Source, pet);
            pet.GetComp<SummonComp>().Setup(ctx.Source.Id, def);
            if (def.OnSpawn != null && def.OnSpawn.Length > 0) ctx.World.Deliver(def.OnSpawn, ctx.Source, pet, ctx.SnapshotAtk);
            ctx.World.PublishSpawn(id, "summon");
        }
    }
}
