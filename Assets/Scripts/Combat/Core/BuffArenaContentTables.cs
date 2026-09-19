using System;

namespace Combat.Core
{
    /// Pure C# baseline table for the Buff Arena content. It is the source of truth for
    /// the .NET regression suite and the seed for the generated ScriptableObject assets
    /// (see BuffArenaDatabaseBuilder). The Unity runtime reads the baked assets instead
    /// of this table, so tuning normally happens in the Inspector.
    public static class BuffArenaContentTables
    {
        public static void Populate(BuffArenaData data)
        {
            RegisterCues(data);
            RegisterProjectiles(data);
            RegisterAoes(data);
            RegisterTimelines(data);
            RegisterSkills(data);
            RegisterActors(data);
        }

        static void RegisterActors(BuffArenaData data)
        {
            data.Actors.Add(new BuffArenaActorDef
            {
                BlueprintId = BuffArenaIds.PlayerBlueprint,
                ViewBlueprintId = "buff_player_view",
                IsPlayer = true,
                TeamId = 1,
                BodyRadius = .25f,
                AmmoCapacity = 60,
                Atk = 50f,
                AtkRandomRange = 20f,
                MoveSpeed = 3f,
                ActionSpeed = 1f,
                CritRate = .05f
            });
            data.Actors.Add(new BuffArenaActorDef
            {
                BlueprintId = BuffArenaIds.EnemyBlueprint,
                ViewBlueprintId = "buff_enemy_view",
                TeamId = 2,
                BodyRadius = .25f,
                MaxHp = 50f,
                MaxHpPerIndex = 2f,
                Atk = 15f,
                AtkRandomRange = 15f,
                AtkPerIndex = 1f,
                ActionSpeed = 1f,
                CritRate = .05f,
                UseLegacySpeedCurve = true
            });
            data.Actors.Add(new BuffArenaActorDef
            {
                BlueprintId = BuffArenaIds.BarrelBlueprint,
                ViewBlueprintId = "buff_barrel_view",
                TeamId = 0,
                BodyRadius = .25f
            });
        }

        static void RegisterCues(BuffArenaData data)
        {
            RegisterCue(data, BuffArenaIds.CueMuzzleValue, "fx_muzzle", "MuzzleFlash");
            RegisterCue(data, BuffArenaIds.CueHeartValue, "fx_heart", "Heart");
            RegisterCue(data, BuffArenaIds.CueRollFireValue, "fx_roll_fire", "Fire_B");
            RegisterCue(data, BuffArenaIds.CueHitValue, "fx_hit", "HitEffect_A");
            RegisterCue(data, BuffArenaIds.CueShieldValue, "fx_shield", "HitEffect_B");
            RegisterCue(data, BuffArenaIds.CueExplosionValue, "fx_explosion", "Explosion_A");
            RegisterCue(data, BuffArenaIds.CueStarValue, "fx_star", "Star_B");
            RegisterCue(data, BuffArenaIds.CueShockwaveValue, "fx_shockwave", "ShockWave");
        }

        static void RegisterCue(BuffArenaData data, int id, string prefabKey, string name)
        {
            data.Cues.Register(new CueDef { CueId = id, PrefabKey = prefabKey, SfxKey = name, LifeTime = .8f });
        }

        static void RegisterProjectiles(BuffArenaData data)
        {
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileNormalValue,
                Speed = 6f,
                Lifetime = 10f,
                HitRadius = .1f,
                MaxHits = 1,
                SameTargetDelay = .1f,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_normal_view",
                OnHit = DamageBag(1f, true),
                OnExpire = CueBag(BuffArenaIds.CueHitValue)
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileEnemyValue,
                Speed = 6f,
                Lifetime = 10f,
                HitRadius = .1f,
                MaxHits = 1,
                SameTargetDelay = .1f,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_enemy_view",
                OnHit = DamageBag(1f, true),
                OnExpire = CueBag(BuffArenaIds.CueHitValue)
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileHomingValue,
                Speed = 3f,
                Lifetime = 100f,
                HitRadius = .1f,
                MaxHits = 1,
                SameTargetDelay = .1f,
                HomingRate = 36000f,
                HomingAcquireRadius = 14f,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_normal_view",
                OnHit = DamageBag(1f, true),
                OnExpire = CueBag(BuffArenaIds.CueHitValue)
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileBoomerangValue,
                Speed = 5f,
                Lifetime = 10f,
                HitRadius = .5f,
                MaxHits = 99999,
                SameTargetDelay = .5f,
                Motion = ProjectileMotionKind.ReturnToOwner,
                MotionParam = 1f,
                HitOwnerOnReturn = true,
                RemoveOnObstacle = false,
                ViewBlueprintId = "buff_projectile_boomerang_view",
                OnHit = DamageBag(1f, true),
                OnOwnerHit = CueBag(BuffArenaIds.CueHeartValue, "Body")
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileTeleportValue,
                Speed = 6f,
                Lifetime = 3f,
                HitRadius = .1f,
                MaxHits = 1,
                Motion = ProjectileMotionKind.Accelerate,
                MotionParam = 5f,
                TrackOwner = true,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_teleport_view",
                OnHit = DamageBag(.6f, false),
                OnExpire = CueBag(BuffArenaIds.CueStarValue)
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileBombValue,
                Speed = 3f,
                Lifetime = 2f,
                HitRadius = .1f,
                MaxHits = 1,
                RemoveOnObstacle = true,
                // Source SetBombBouncing: touchdown at 3T/6, 5T/6 and T-0.001 with T = 2s.
                GroundPhaseAt = new[] { 1.0f, 1.6667f, 1.999f },
                ViewBlueprintId = "buff_projectile_bomb_view",
                OnHit = new IEffect[] { new SpawnAoeEffect(BuffArenaIds.AoeExplosionValue, false, 1.5f, .02f) },
                OnExpire = new IEffect[]
                {
                    new SpawnAoeEffect(BuffArenaIds.AoeStayingBombValue, false, .1f, 3f),
                    new SpawnAoeEffect(BuffArenaIds.AoeExplosionValue, false, 1.5f, .02f)
                },
                OnObstacle = new IEffect[] { new SpawnAoeEffect(BuffArenaIds.AoeExplosionValue, false, 1.5f, .02f) }
            });
        }

        static void RegisterAoes(BuffArenaData data)
        {
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeShieldValue,
                Radius = 1.5f,
                Duration = 0f,
                TrackProjectiles = true,
                ProjectileAbsorbForce = 0f,
                ViewBlueprintId = "buff_aoe_shield_view"
            });
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeMonkeyValue,
                Radius = .25f,
                Duration = 100f,
                Motion = AoeMotionKind.Forward,
                MoveSpeed = .1f,
                RemoveOnObstacle = true,
                TrackOccupancy = true,
                TrackProjectiles = true,
                ProjectileAbsorbForce = .05f,
                ProjectileRadiusScale = .05f,
                ViewBlueprintId = "buff_aoe_monkey_view",
                OnEnter = DamageBag(.2f, true)
            });
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeBlackHoleValue,
                Radius = 2f,
                Duration = 1f,
                PulseInterval = .02f,
                TrackOccupancy = true,
                ViewBlueprintId = "buff_aoe_blackhole_view",
                OnStay = new IEffect[] { new PullToPointEffect() }
            });
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeExplosionValue,
                Radius = 1.5f,
                Duration = .02f,
                PulseOnSpawn = true,
                OnPulse = new IEffect[]
                {
                    new DamageEffect { Coeff = .1f, CanCrit = true, CritMul = 1.8f,
                        CritChance = .05f, FireOnHurted = false, DirectDamage = true },
                    new HurtFeedbackEffect(),
                    new PlayBuffArenaCueEffect(BuffArenaIds.CueHitValue, "Body", target: true)
                },
                OnExpire = CueBag(BuffArenaIds.CueExplosionValue)
            });
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeStayingBombValue,
                Radius = .1f,
                Duration = 3f,
                ViewBlueprintId = "buff_aoe_stayingbomb_view",
                OnExpire = new IEffect[]
                {
                    new SpawnAoeEffect(BuffArenaIds.AoeExplosionValue, false, 1.5f, .02f)
                }
            });
        }

        static void RegisterTimelines(BuffArenaData data)
        {
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineFireValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueMuzzleValue, "Muzzle"),
                    new SpawnProjectileEffect(BuffArenaIds.ProjectileNormalValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineReloadValue, 1.15f, "Reload", true, true,
                Payload(1.1f, new RefillAmmoEffect(60))));
            // Source skill_cloakBoomerang sets canUseSkill false for the whole cast.
            var boomerang = Timeline(BuffArenaIds.TimelineBoomerangValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueHeartValue, "Head"),
                    new SpawnProjectileEffect(BuffArenaIds.ProjectileBoomerangValue)));
            boomerang.AllowSkill = false;
            data.Timelines.Register(boomerang);
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineTeleportValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueMuzzleValue, "Muzzle"),
                    new SpawnProjectileEffect(BuffArenaIds.ProjectileTeleportValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineHomingValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueMuzzleValue, "Muzzle"),
                    new SpawnProjectileEffect(BuffArenaIds.ProjectileHomingValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineGrenadeValue, .5f, "Fire", true, true,
                Payload(.1f, new SpawnProjectileEffect(BuffArenaIds.ProjectileBombValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineBarrelValue, .5f, "Fire", true, true,
                Payload(.1f, new SpawnBuffArenaBarrelEffect())));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineMonkeyValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueMuzzleValue, "Muzzle"),
                    new SpawnAoeEffect(BuffArenaIds.AoeMonkeyValue, false, .25f, 100f, .5f))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineEnemyValue, .5f, "Fire", true, true,
                Payload(.1f, new SpawnProjectileEffect(BuffArenaIds.ProjectileEnemyValue))));

            var roll = Timeline(BuffArenaIds.TimelineRollValue, .9f, "Roll", false, false,
                Payload(0f, new PlayBuffArenaCueEffect(BuffArenaIds.CueRollFireValue, "Body", "roll_fire", false, false, true)),
                Payload(.8f, new PlayBuffArenaCueEffect(BuffArenaIds.CueRollFireValue, "Body", "roll_fire", false, false, false, true),
                    new PlayBuffArenaCueEffect(BuffArenaIds.CueShockwaveValue, "Body")));
            roll.Clips = new[]
            {
                new TimelineClip { Start = .1f, End = .8f, Kind = ClipKind.IFrame },
                // Source skill_roll: CasterForceMove(2.0f, 0.5f) inside the 0.2s-0.8s window.
                new TimelineClip { Start = .2f, End = .8f, Kind = ClipKind.Move, MoveZ = 2.0f }
            };
            data.Timelines.Register(roll);
        }

        static void RegisterSkills(BuffArenaData data)
        {
            AddSkill(data, BuffArenaIds.Fire, BuffArenaIds.FireTimeline, BuffArenaIds.Fire1, 1, "Fire");
            AddSkill(data, BuffArenaIds.Roll, BuffArenaIds.RollTimeline, BuffArenaIds.RollInput, 0, "Roll");
            AddSkill(data, BuffArenaIds.Monkey, BuffArenaIds.MonkeyTimeline, BuffArenaIds.MonkeyInput, 3, "Fire");
            AddSkill(data, BuffArenaIds.Homing, BuffArenaIds.HomingTimeline, BuffArenaIds.HomingInput, 2, "Fire");
            AddSkill(data, BuffArenaIds.Boomerang, BuffArenaIds.BoomerangTimeline, BuffArenaIds.Fire2, 0, "Fire");
            // Teleport bullet hands over to the warp skill while its projectile is alive.
            AddSkill(data, BuffArenaIds.Teleport, BuffArenaIds.TeleportTimeline, BuffArenaIds.Fire4, 0, "Fire",
                requiresTrackedProjectile: true, warpSkill: BuffArenaIds.TeleportWarp);
            AddSkill(data, BuffArenaIds.TeleportWarp, BuffArenaIds.TeleportTimeline, new InputToken(""), 0, "Fire");
            AddSkill(data, BuffArenaIds.Grenade, BuffArenaIds.GrenadeTimeline, BuffArenaIds.Fire3, 0, "Fire");
            AddSkill(data, BuffArenaIds.Barrel, BuffArenaIds.BarrelTimeline, BuffArenaIds.Fire5, 0, "Fire");
            AddSkill(data, BuffArenaIds.Reload, BuffArenaIds.ReloadTimeline, new InputToken(""), 0, "Reload");
            AddSkill(data, BuffArenaIds.SkillEnemy, BuffArenaIds.TimelineEnemy, new InputToken(""), 0, "Fire");
        }

        static void AddSkill(BuffArenaData data, SkillNodeId id, TimelineId timeline, InputToken input,
            int ammoCost, string animation, bool requiresTrackedProjectile = false,
            SkillNodeId warpSkill = default(SkillNodeId))
        {
            data.Skills.Add(new BuffArenaSkill
            {
                Id = id,
                Timeline = timeline,
                Input = input,
                AmmoCost = ammoCost,
                AnimatorState = animation,
                RequiresTrackedProjectile = requiresTrackedProjectile,
                WarpSkillId = warpSkill
            });
        }

        static TimelineSO Timeline(int id, float duration, string animation, bool allowMove, bool allowRotate,
            params TimelinePayload[] payloads)
        {
            return new TimelineSO
            {
                Id = new TimelineId(id),
                Duration = duration,
                AnimatorState = animation,
                AllowMove = allowMove,
                AllowRotate = allowRotate,
                Payloads = payloads ?? Array.Empty<TimelinePayload>(),
                Clips = Array.Empty<TimelineClip>()
            };
        }

        static TimelinePayload Payload(float time, params IEffect[] effects)
        {
            return new TimelinePayload { Time = time, Effects = effects ?? Array.Empty<IEffect>() };
        }

        static IEffect[] DamageBag(float coefficient, bool canCrit)
        {
            return new IEffect[]
            {
                new DamageEffect { Coeff = coefficient, CanCrit = canCrit, CritMul = 1.8f,
                    CritChance = canCrit ? .05f : 0f, FireOnHurted = false, DirectDamage = true },
                new HurtFeedbackEffect(),
                new PlayBuffArenaCueEffect(BuffArenaIds.CueHitValue, "Body", target: true)
            };
        }

        static IEffect[] CueBag(int cueId, string anchor = "")
        {
            return new IEffect[] { new PlayBuffArenaCueEffect(cueId, anchor, point: anchor.Length == 0) };
        }
    }
}
