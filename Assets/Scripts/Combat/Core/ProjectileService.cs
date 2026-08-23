using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class ProjectileService
    {
        readonly CombatWorld _world;
        readonly IntentQueue _intents;
        readonly ProjectileSpecLibrary _specs;
        readonly CombatActorFactory _factory;
        readonly List<EntityId> _activeProjectiles = new List<EntityId>(32);

        public ProjectileService(
            CombatWorld world,
            IntentQueue intents,
            ProjectileSpecLibrary specs,
            CombatActorFactory factory)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
            _specs = specs ?? throw new ArgumentNullException(nameof(specs));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public IReadOnlyList<EntityId> ActiveProjectiles => _activeProjectiles;

        public void Tick()
        {
            _intents.Drain<SpawnProjectileIntent>(SpawnOne);

            // 清理已销毁 id
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (!_world.TryGetActor(_activeProjectiles[i], out _))
                    _activeProjectiles.RemoveAt(i);
            }
        }

        void SpawnOne(SpawnProjectileIntent intent)
        {
            if (!_specs.TryGet(intent.SpecValue, out var spec))
            {
                Console.WriteLine($"[ProjectileService] unknown spec {intent.SpecValue}");
                return;
            }

            if (!_world.TryGetActor(intent.Owner, out var owner))
                return;

            var ownerTf = owner.GetComp<TransformComp>();
            int ownerTeam = 0;
            if (owner.TryGetComp<TeamComp>(out var team))
                ownerTeam = team.Team;

            // 方向：MVP 用 spec 本地方向；有 Yaw 时可按朝向旋转
            var dir = ownerTf.LocalToWorld(spec.DirX, spec.DirY, spec.DirZ);
            float lenSq = dir.X * dir.X + dir.Y * dir.Y + dir.Z * dir.Z;
            if (lenSq < 1e-8f) dir = new SimVec3(1f, 0f, 0f);
            else
            {
                float inv = 1f / MathF.Sqrt(lenSq);
                dir = new SimVec3(dir.X * inv, dir.Y * inv, dir.Z * inv);
            }

            var spawnOffset = ownerTf.LocalToWorld(
                spec.SpawnOffsetX, spec.SpawnOffsetY, spec.SpawnOffsetZ);
            var pos = ownerTf.Position + spawnOffset;

            var vel = new SimVec3(dir.X * spec.Speed, dir.Y * spec.Speed, dir.Z * spec.Speed);

            int skillValue = 0;
            if (owner.TryGetComp<SkillDirectorComp>(out var dirComp))
                skillValue = dirComp.CurrentSkill.Value;

            var ctx = new ProjectileSpawnContext
            {
                IsValid = true,
                Owner = intent.Owner,
                OwnerTeam = ownerTeam,
                Position = pos,
                Velocity = vel,
                Spec = spec,
                SourceSkillValue = skillValue
            };

            _factory.SetPendingProjectile(ctx);
            var id = _world.SpawnActor(new ActorSpawnSpec("projectile"));
            if (_world.TryGetActor(id, out var proj))
            {
                ProjectileSetup.Apply(proj, ctx);
                _activeProjectiles.Add(id);
            }
        }
    }

    public sealed class HitDetectService
    {
        readonly CombatWorld _world;
        readonly IntentQueue _intents;
        readonly ProjectileService _projectiles;
        readonly List<Actor> _hurtScratch = new List<Actor>(32);

        public HitDetectService(
            CombatWorld world,
            IntentQueue intents,
            ProjectileService projectiles)
        {
            _world = world;
            _intents = intents;
            _projectiles = projectiles;
        }

        public void Tick()
        {
            CollectHurtboxes();

            var list = _projectiles.ActiveProjectiles;
            for (int i = 0; i < list.Count; i++)
            {
                if (!_world.TryGetActor(list[i], out var proj))
                    continue;
                if (!proj.TryGetComp<ProjectileContactComp>(out var contact))
                    continue;
                if (!proj.TryGetComp<TransformComp>(out var ptf))
                    continue;
                if (!proj.TryGetComp<ProjectileHitRecordComp>(out var record))
                    continue;

                for (int h = 0; h < _hurtScratch.Count; h++)
                {
                    var target = _hurtScratch[h];
                    if (target.Id == proj.Id)
                        continue;
                    if (target.Id == contact.Owner)
                        continue; // 不打主人

                    var hurt = target.GetComp<HurtboxComp>();
                    if (!hurt.CanBeHit)
                        continue;

                    int srcTeam = contact.Team; // 生成投射物时写入的是 Owner 的 TeamComp
                    int dstTeam = 0;
                    if (target.TryGetComp<TeamComp>(out var team))
                        dstTeam = team.Team;
                    else
                        continue; // 无阵营则不可被弹道打（或按需放行）
                    if (dstTeam == srcTeam)
                        continue;

                    var ttf = target.GetComp<TransformComp>();
                    if (!Overlap(ptf.Position, contact.Radius, ttf.Position, hurt.Radius))
                        continue;

                    if (record.HasHit(target.Id))
                        continue;

                    record.Record(target.Id);
                    _intents.Post(new HitIntent(
                        proj.Id,
                        target.Id,
                        contact.Owner,
                        contact.AttackSpecValue,
                        contact.SourceSkillValue));

                    if (!contact.Pierce)
                    {
                        _intents.Post(new DespawnEntityIntent(proj.Id));
                        break;
                    }
                }
            }
        }

        void CollectHurtboxes()
        {
            _hurtScratch.Clear();
            var actors = ((EntityRegistry)_world.Registry).CopyActiveActors();
            // 若 Registry 未暴露 CopyActiveActors 给外部，给 CombatWorld 加方法：
            // public List<Actor> CopyActiveActors() => _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].TryGetComp<HurtboxComp>(out _))
                    _hurtScratch.Add(actors[i]);
            }
        }

        static bool Overlap(in SimVec3 a, float ra, in SimVec3 b, float rb)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            float r = ra + rb;
            return dx * dx + dy * dy + dz * dz <= r * r;
        }
    }

    public sealed class DespawnService
    {
        readonly CombatWorld _world;
        readonly IntentQueue _intents;

        public DespawnService(CombatWorld world, IntentQueue intents)
        {
            _world = world;
            _intents = intents;
        }

        public void Tick()
        {
            _intents.Drain<DespawnEntityIntent>(req => _world.RequestDespawn(req.Target));
        }
    }


    public static class ProjectileSetup
    {
        public static void Apply(Actor projectile, in ProjectileSpawnContext ctx)
        {
            var tf = projectile.GetComp<TransformComp>();
            tf.Teleport(ctx.Position);
            var move = projectile.GetComp<ProjectileMoveComp>();
            move.SetVelocity(ctx.Velocity);
            var life = projectile.GetComp<ProjectileLifetimeComp>();
            life.Arm(ctx.Spec.Lifetime);
            var contact = projectile.GetComp<ProjectileContactComp>(); // todo 
            contact.Setup(
                ctx.Owner,
                ctx.OwnerTeam,
                ctx.Spec.Radius,
                ctx.Spec.AttackSpecValue,
                ctx.Spec.Pierce,
                ctx.SourceSkillValue);
            projectile.GetComp<ProjectileHitRecordComp>().Clear();
        }
    }

    public struct ProjectileSpawnContext
    {
        public bool IsValid;
        public EntityId Owner;
        public int OwnerTeam;
        public SimVec3 Position;
        public SimVec3 Velocity;
        public ProjectileSpec Spec;
        public int SourceSkillValue;
    }

}
