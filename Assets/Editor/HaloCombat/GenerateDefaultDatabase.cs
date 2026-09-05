using System;
using System.IO;
using Combat.Config;
using Combat.Core;
using UnityEditor;
using UnityEngine;

namespace Combat.EditorTools
{
    public static class GenerateDefaultDatabase
    {
        const string Root = "Assets/Combat/Config/Generated";

        [MenuItem("Combat/Generate Default Database From Code")]
        public static void Generate()
        {
            Write();
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Generate", "Wrote " + Root + "/CombatDatabase.asset", "OK");
        }

        public static void GenerateBatch() => Write();

        static void Write()
        {
            Directory.CreateDirectory(Root);
            var baked = new CodeCombatContent().Bake();

            var damage = Make<DamageEffectAsset>("Damage_G1");
            damage.Coeff = 1f; damage.CanCrit = true; damage.UseSnapshotAtk = true; damage.HitstopFrames = 3;
            var stun = Make<HitStunAsset>("Stun_G1"); stun.Duration = 0.35f;
            var knockback = Make<KnockbackAsset>("Knockback_G1"); knockback.Distance = 0.4f;
            var profile = Make<HitProfileAsset>("Melee_G1");
            profile.Damage = damage; profile.Stun = stun; profile.Knockback = knockback;

            var cueSlash = Make<PlayCueAsset>("Cue_G1_Slash"); cueSlash.CueId = 101; cueSlash.CueName = "G1_Slash";
            var spawnFireball = Make<SpawnProjectileAsset>("Spawn_Fireball"); spawnFireball.SpecId = CombatIds.Fireball;
            var spawnHoming = Make<SpawnProjectileAsset>("Spawn_HomingBolt"); spawnHoming.SpecId = CombatIds.HomingBolt;
            var spawnGround = Make<SpawnAoeAsset>("Spawn_FireGround"); spawnGround.SpecId = CombatIds.FireGround;
            var cueG2 = Make<PlayCueAsset>("Cue_G2"); cueG2.CueId = 102; cueG2.CueName = "G2";

            var tlG1 = Make<SkillTimelineAsset>("TL_G1");
            tlG1.TimelineIdValue = TimelineId.TL_G1.Value; tlG1.Duration = 0.55f;
            tlG1.Clips = new[]
            {
                new TimelineClipAsset { Start = 0.12f, End = 0.40f, Kind = ClipKind.CancelTag },
                new TimelineClipAsset { Start = 0.08f, End = 0.28f, Kind = ClipKind.Move, MoveX = 0.6f },
                new TimelineClipAsset { Start = 0.18f, End = 0.30f, Kind = ClipKind.Hitbox, HitRadius = 0.8f, HitProfile = profile }
            };
            tlG1.Payloads = new[]
            {
                new TimelinePayloadAsset { Time = 0.18f, Effects = new EffectAsset[] { cueSlash } },
                new TimelinePayloadAsset { Time = 0.22f, Effects = new EffectAsset[] { spawnFireball } }
            };

            var tlG2 = Make<SkillTimelineAsset>("TL_G2");
            tlG2.TimelineIdValue = TimelineId.TL_G2.Value; tlG2.Duration = 0.40f;
            tlG2.Clips = new[]
            {
                new TimelineClipAsset { Start = 0f, End = 0.20f, Kind = ClipKind.CancelTag },
                new TimelineClipAsset { Start = 0f, End = 0.12f, Kind = ClipKind.Move, MoveX = 0.25f }
            };
            tlG2.Payloads = new[] { new TimelinePayloadAsset { Time = 0.05f, Effects = new EffectAsset[] { cueG2, spawnGround } } };

            var tlDodge = Make<SkillTimelineAsset>("TL_Dodge");
            tlDodge.TimelineIdValue = TimelineId.TL_Dodge.Value; tlDodge.Duration = 0.40f;
            tlDodge.Clips = new[]
            {
                new TimelineClipAsset { Start = 0f, End = 0.28f, Kind = ClipKind.Move, MoveX = 1.2f },
                new TimelineClipAsset { Start = 0.04f, End = 0.22f, Kind = ClipKind.IFrame },
                new TimelineClipAsset { Start = 0.24f, End = 0.40f, Kind = ClipKind.CancelTag }
            };

            var tlHoming = Make<SkillTimelineAsset>("TL_Homing");
            tlHoming.TimelineIdValue = TimelineId.TL_Homing.Value; tlHoming.Duration = 0.20f;
            tlHoming.Clips = Array.Empty<TimelineClipAsset>();
            tlHoming.Payloads = new[]
            {
                new TimelinePayloadAsset { Time = 0.02f, Effects = new EffectAsset[] { spawnHoming } }
            };

            var burn = Make<DurationSpecAsset>("Burn");
            burn.BuffId = CombatIds.Burn; burn.Duration = 3f; burn.TickInterval = 1f; burn.MaxStacks = 3; burn.Stack = StackPolicy.AddStack;
            var burnDamage = Make<DamageEffectAsset>("Damage_BurnTick");
            burnDamage.Coeff = 0.2f; burnDamage.CanCrit = false; burnDamage.UseSnapshotAtk = true; burnDamage.ScaleByBuffStacks = true;
            burn.OnPeriod = new EffectAsset[] { burnDamage };
            var applyBurn = Make<ApplyDurationAsset>("Apply_Burn"); applyBurn.Spec = burn;

            var fireball = Make<ProjectileDefAsset>("Fireball");
            fireball.SpecId = CombatIds.Fireball; fireball.Speed = 14f; fireball.Lifetime = 2f; fireball.HitRadius = 0.3f; fireball.MaxHits = 1; fireball.SnapshotAtk = true; fireball.SpawnForward = 0.4f;
            var fireballDamage = Make<DamageEffectAsset>("Damage_Fireball"); fireballDamage.Coeff = 1f; fireballDamage.CanCrit = true; fireballDamage.UseSnapshotAtk = true;
            var cueHit = Make<PlayCueAsset>("Cue_FireballHit"); cueHit.CueId = CombatIds.CueFireballHit; cueHit.CueName = "FireballHit";
            fireball.OnHit = new EffectAsset[] { fireballDamage, stun, knockback, applyBurn, cueHit };

            var homing = Make<ProjectileDefAsset>("HomingBolt");
            homing.SpecId = CombatIds.HomingBolt; homing.Speed = 8f; homing.Lifetime = 2.5f; homing.HitRadius = 0.35f; homing.MaxHits = 1; homing.HomingRate = 270f; homing.SpawnForward = 0.2f;
            var homingDamage = Make<DamageEffectAsset>("Damage_Homing"); homingDamage.Coeff = 1f; homingDamage.CanCrit = false; homing.OnHit = new EffectAsset[] { homingDamage, stun, knockback };

            var ground = Make<AoeDefAsset>("FireGround");
            ground.SpecId = CombatIds.FireGround; ground.Radius = 1.3f; ground.Duration = 2f; ground.PulseInterval = 0.45f; ground.PulseOnSpawn = true; ground.TrackOccupancy = false; ground.CueId = CombatIds.CueFireGround; ground.OnPulse = new EffectAsset[] { applyBurn };

            var auraSlow = Make<DurationSpecAsset>("AuraSlow");
            auraSlow.BuffId = CombatIds.AuraSlow; auraSlow.Duration = 0f; auraSlow.Stack = StackPolicy.Independent; auraSlow.ModAttr = AttrId.MoveSpeed; auraSlow.ModOp = ModOp.Mul; auraSlow.ModValue = 0.5f;
            var applyAura = Make<ApplyDurationAsset>("Apply_AuraSlow"); applyAura.Spec = auraSlow;
            var dispel = Make<DispelAsset>("Dispel_BySource"); dispel.Mode = DispelMode.BySource;
            var aura = Make<AoeDefAsset>("AuraField");
            aura.SpecId = CombatIds.AuraField; aura.Radius = 1.2f; aura.Duration = 8f; aura.PulseInterval = 0f; aura.PulseOnSpawn = false; aura.TrackOccupancy = true; aura.OnEnter = new EffectAsset[] { applyAura }; aura.OnExit = new EffectAsset[] { dispel };

            var summon = Make<SummonDefAsset>("MeleeSummon"); summon.SpecId = CombatIds.MeleeSummon; summon.FollowRange = 2f; summon.AcquireRadius = 8f; summon.Recipe = TreeRecipeKind.SummonMelee;

            var combo = Make<ComboTableAsset>("WarriorCombo");
            combo.Entries = new[]
            {
                new ComboEntryAsset { PreSkills = Array.Empty<int>(), InputAction = "Attack", ToSkill = SkillNodeId.G1.Value, Timeline = TimelineId.TL_G1.Value },
                new ComboEntryAsset { PreSkills = new[] { SkillNodeId.G1.Value }, InputAction = "Attack", RequiredTags = new[] { CommonTags.Cancel.Value }, Priority = 10, ToSkill = SkillNodeId.G2.Value, Timeline = TimelineId.TL_G2.Value },
                new ComboEntryAsset { PreSkills = new[] { SkillNodeId.Dodge.Value }, InputAction = "Attack", RequiredTags = new[] { CommonTags.Cancel.Value }, Priority = 5, ToSkill = SkillNodeId.G1.Value, Timeline = TimelineId.TL_G1.Value }
            };

            var motor = Make<CharacterMotorAsset>("HeroMotor");
            var cues = Make<CueLibraryAsset>("CombatCues");
            cues.Entries = new[]
            {
                new CueLibraryAsset.Entry { CueId = 101, PrefabKey = "fx_g1_slash", LifeTime = 0.4f },
                new CueLibraryAsset.Entry { CueId = 102, PrefabKey = "fx_g2", LifeTime = 0.3f },
                new CueLibraryAsset.Entry { CueId = CombatIds.CueFireballHit, PrefabKey = "fx_fireball_hit", LifeTime = 0.5f },
                new CueLibraryAsset.Entry { CueId = CombatIds.CueFireGround, PrefabKey = "fx_fire_ground", LifeTime = 2f }
            };

            var database = Make<CombatDatabaseAsset>("CombatDatabase");
            database.Combo = combo;
            database.Timelines = new[] { tlG1, tlG2, tlDodge, tlHoming };
            database.Projectiles = new[] { fireball, homing };
            database.Aoes = new[] { ground, aura };
            database.Buffs = new[] { burn, auraSlow };
            database.Summons = new[] { summon };
            database.Cues = cues;
            database.Motor = motor;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _ = baked;
        }

        static T Make<T>(string name) where T : ScriptableObject
        {
            string path = Root + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }

    public static class ValidateMenu
    {
        [MenuItem("Combat/Validate Database")]
        public static void Validate()
        {
            var database = AssetDatabase.LoadAssetAtPath<CombatDatabaseAsset>("Assets/Combat/Config/Generated/CombatDatabase.asset");
            if (database == null) throw new InvalidOperationException("Generate database first");
            var report = CombatValidator.Validate(database.BakeAll());
            if (report.HasError) throw new InvalidOperationException(report.ToString());
            ValidateCodeSoParity(database);
            if (!Application.isBatchMode) EditorUtility.DisplayDialog("Validate", "OK", "OK");
            Debug.Log("[HaloCombat] Database validation passed.");
        }

        static void ValidateCodeSoParity(CombatDatabaseAsset database)
        {
            var code = new CodeCombatContent().Bake();
            var so = database.BakeAll();
            if (code.Combo == null || so.Combo == null || code.Combo.Entries.Length != so.Combo.Entries.Length)
                throw new InvalidOperationException("Code/SO combo entry count mismatch");

            for (int i = 0; i < code.Combo.Entries.Length; i++)
            {
                var a = code.Combo.Entries[i];
                var b = so.Combo.Entries[i];
                if (a.Priority != b.Priority || a.ToSkill != b.ToSkill || a.Timeline != b.Timeline ||
                    !a.Input.Equals(b.Input) || !Same(a.PreSkills, b.PreSkills) || !Same(a.RequiredTags, b.RequiredTags))
                    throw new InvalidOperationException("Code/SO combo entry mismatch at index " + i);
            }

            RequireTimeline(code, so, TimelineId.TL_G1);
            RequireTimeline(code, so, TimelineId.TL_G2);
            RequireTimeline(code, so, TimelineId.TL_Dodge);
            RequireTimeline(code, so, TimelineId.TL_Homing);
            RequireProjectile(code, so, CombatIds.Fireball);
            RequireProjectile(code, so, CombatIds.HomingBolt);
            RequireAoe(code, so, CombatIds.FireGround);
            RequireAoe(code, so, CombatIds.AuraField);
            RequireSummon(code, so, CombatIds.MeleeSummon);
            if (Math.Abs(code.Motor.Gravity - so.Motor.Gravity) > 1e-4f ||
                Math.Abs(code.Motor.JumpSpeed - so.Motor.JumpSpeed) > 1e-4f ||
                Math.Abs(code.Motor.AirSteer - so.Motor.AirSteer) > 1e-4f ||
                Math.Abs(code.Motor.GroundY - so.Motor.GroundY) > 1e-4f ||
                Math.Abs(code.Motor.StickDeadzone - so.Motor.StickDeadzone) > 1e-4f)
                throw new InvalidOperationException("Code/SO motor configuration mismatch");
        }

        static bool Same(int[] a, int[] b)
        {
            if (a == null) a = Array.Empty<int>();
            if (b == null) b = Array.Empty<int>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static bool Same(SkillNodeId[] a, SkillNodeId[] b)
        {
            if (a == null) a = Array.Empty<SkillNodeId>();
            if (b == null) b = Array.Empty<SkillNodeId>();
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static void RequireTimeline(BakedCombatData code, BakedCombatData so, TimelineId id)
        {
            if (!code.Timelines.TryGet(id, out _) || !so.Timelines.TryGet(id, out _))
                throw new InvalidOperationException("Code/SO timeline missing: " + id.Value);
        }

        static void RequireProjectile(BakedCombatData code, BakedCombatData so, int id)
        {
            if (!code.Projectiles.TryGet(id, out _) || !so.Projectiles.TryGet(id, out _))
                throw new InvalidOperationException("Code/SO projectile missing: " + id);
        }

        static void RequireAoe(BakedCombatData code, BakedCombatData so, int id)
        {
            if (!code.Aoes.TryGet(id, out _) || !so.Aoes.TryGet(id, out _))
                throw new InvalidOperationException("Code/SO AOE missing: " + id);
        }

        static void RequireSummon(BakedCombatData code, BakedCombatData so, int id)
        {
            if (!code.Summons.TryGet(id, out _) || !so.Summons.TryGet(id, out _))
                throw new InvalidOperationException("Code/SO summon missing: " + id);
        }
    }
}
