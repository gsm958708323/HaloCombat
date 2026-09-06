using System;
using System.IO;
using Combat.Config;
using Combat.Core;
using Combat.Unity.Game;
using Combat.Unity.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.EditorTools
{
    public static class GenerateDefaultDatabase
    {
        const string Root = "Assets/Combat/Config/Generated";
        const string DatabaseFolder = "Database";
        const string CharactersFolder = "Characters";
        const string SkillsFolder = "Skills";
        const string TimelinesFolder = "Timelines";
        const string CombosFolder = "Combos";
        const string BehaviorTreesFolder = "BehaviorTrees";
        const string EffectsFolder = "Effects";
        const string HitProfilesFolder = "HitProfiles";
        const string ProjectilesFolder = "Projectiles";
        const string AoesFolder = "Aoes";
        const string SummonsFolder = "Summons";
        const string CuesFolder = "Cues";
        const string MotorFolder = "Motor";
        const string SpawnsFolder = "Spawns";
        const string MiscFolder = "Misc";

        public static string DatabaseAssetPath => GeneratedPath<CombatDatabaseAsset>("CombatDatabase");

        [MenuItem("Combat/Generate Default Database From Code")]
        public static void Generate()
        {
            Write();
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Generate", "Wrote " + GeneratedPath<CombatDatabaseAsset>("CombatDatabase"), "OK");
        }

        public static void GenerateBatch() => Write();

        static void Write()
        {
            Directory.CreateDirectory(ProjectFilePath(Root));
            MigrateLegacyAssets();
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

            BuildRoleConfiguration(database, burn, applyBurn, stun, knockback, spawnFireball, spawnGround, spawnHoming);
            EnsureInputActions();
            EnsureArenaSpawns();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BindRuntimeScenes(database);
            _ = baked;
        }

        static void BuildRoleConfiguration(
            CombatDatabaseAsset database,
            DurationSpecAsset burn,
            ApplyDurationAsset applyBurn,
            HitStunAsset stun,
            KnockbackAsset knockback,
            SpawnProjectileAsset spawnFireball,
            SpawnAoeAsset spawnGround,
            SpawnProjectileAsset spawnHoming)
        {
            var skills = new System.Collections.Generic.List<SkillDefinitionAsset>();
            var timelines = new System.Collections.Generic.List<SkillTimelineAsset>();

            var sword = BuildSwordSkills(skills, timelines, stun, knockback);
            var fighter = BuildFighterSkills(skills, timelines, stun, knockback);
            var gun = BuildGunSkills(skills, timelines, spawnFireball, spawnGround, spawnHoming, applyBurn, stun, knockback);

            var playerSword = Make<CharacterDefinitionAsset>("Character_Swordsman");
            ConfigureCharacter(playerSword, "swordsman", "剑士", true, true, "swordsman", sword.Skills, sword.Combo, null, sword.Dodge);

            var playerFighter = Make<CharacterDefinitionAsset>("Character_Fighter");
            ConfigureCharacter(playerFighter, "fighter", "拳师", true, true, "fighter", fighter.Skills, fighter.Combo, null, fighter.Dodge);

            var playerGun = Make<CharacterDefinitionAsset>("Character_Gunslinger");
            ConfigureCharacter(playerGun, "gunslinger", "枪手", true, true, "gunslinger", gun.Skills, gun.Combo, null, gun.Dodge);

            var meleeAi = Make<CharacterDefinitionAsset>("Character_MeleeAi");
            ConfigureCharacter(meleeAi, "melee_ai", "徒手敌人", false, false, "melee_ai", fighter.Skills, null, BuildAi("BT_MeleeAi", fighter.Skills[0], fighter.Skills[1], fighter.Skills[2]), null);

            var guard = Make<CharacterDefinitionAsset>("Character_MeleeGuard");
            ConfigureCharacter(guard, "melee_guard", "剑士守卫", false, false, "melee_guard", sword.Skills, null, BuildAi("BT_MeleeGuard", sword.Launcher, sword.AirFollowUp), null);

            var ranged = Make<CharacterDefinitionAsset>("Character_RangedAi");
            ConfigureCharacter(ranged, "ranged_ai", "枪手敌人", false, false, "ranged_ai", gun.Skills, null, BuildAi("BT_RangedAi", gun.Fireball, gun.FireGround), null);

            database.Skills = skills.ToArray();
            database.Characters = new[] { playerSword, playerFighter, playerGun, meleeAi, guard, ranged };
            database.Timelines = timelines.ToArray();
            database.Combo = null;
            var fireball = AssetDatabase.LoadAssetAtPath<ProjectileDefAsset>(GeneratedPath<ProjectileDefAsset>("Fireball"));
            var homing = AssetDatabase.LoadAssetAtPath<ProjectileDefAsset>(GeneratedPath<ProjectileDefAsset>("HomingBolt"));
            var pistolBullet = AssetDatabase.LoadAssetAtPath<ProjectileDefAsset>(GeneratedPath<ProjectileDefAsset>("PistolBullet"));
            database.Projectiles = new[] { fireball, homing, pistolBullet };
            var fireGround = AssetDatabase.LoadAssetAtPath<AoeDefAsset>(GeneratedPath<AoeDefAsset>("FireGround"));
            var auraField = AssetDatabase.LoadAssetAtPath<AoeDefAsset>(GeneratedPath<AoeDefAsset>("AuraField"));
            database.Aoes = new[] { fireGround, auraField };
            EditorUtility.SetDirty(database);
        }

        static void ConfigureCharacter(
            CharacterDefinitionAsset asset,
            string id,
            string displayName,
            bool player,
            bool selectable,
            string viewId,
            SkillDefinitionAsset[] skills,
            ComboTableAsset combo,
            BtNodeAsset tree,
            SkillDefinitionAsset dodge)
        {
            asset.BlueprintId = id;
            asset.DisplayName = displayName;
            asset.IsPlayer = player;
            asset.PlayerSelectable = selectable;
            asset.ViewBlueprintId = viewId;
            asset.Skills = skills;
            asset.ComboTable = combo;
            asset.BehaviorTree = tree;
            asset.DodgeSkill = dodge;
            asset.DodgeInputAction = "Dodge";
            asset.JumpInputAction = "Jump";
            asset.AcquireRadius = id == "ranged_ai" ? 10f : 8f;
            asset.AttackRange = id == "ranged_ai" ? 5f : 1.35f;
            asset.FollowRange = id == "ranged_ai" ? 4f : 1.5f;
            asset.LeashRange = id == "melee_guard" ? 6f : 20f;
            asset.PatrolRadius = id == "melee_guard" ? 1.5f : 0f;
            EditorUtility.SetDirty(asset);
        }

        sealed class SkillSet
        {
            public SkillDefinitionAsset[] Skills;
            public ComboTableAsset Combo;
            public SkillDefinitionAsset Dodge;
            public SkillDefinitionAsset Launcher;
            public SkillDefinitionAsset AirFollowUp;
            public SkillDefinitionAsset Fireball;
            public SkillDefinitionAsset FireGround;
        }

        static SkillSet BuildSwordSkills(System.Collections.Generic.List<SkillDefinitionAsset> skills, System.Collections.Generic.List<SkillTimelineAsset> timelines, HitStunAsset stun, KnockbackAsset knockback)
        {
            var light = MakeMeleeSkill("SwordSlash1", 2001, 5001, "剑·横斩", .38f, .10f, .24f, .65f, stun, knockback, null, SkillAnimationMode.Attack);
            var second = MakeMeleeSkill("SwordSlash2", 2002, 5002, "剑·追击斩", .42f, .10f, .27f, .72f, stun, knockback, null, SkillAnimationMode.Attack);
            var finisher = MakeMeleeSkill("SwordFinisher", 2003, 5003, "剑·重斩", .55f, .16f, .34f, .95f, stun, MakeKnockback("SwordFinisher_Knockback", .8f), null, SkillAnimationMode.Attack);
            var launch = MakeMeleeSkill("SwordLauncher", 2004, 5004, "剑·挑空", .50f, .13f, .28f, .78f, stun, knockback, MakeLaunch("SwordLauncher_Launch", 5.5f), SkillAnimationMode.Attack);
            var follow = MakeMeleeSkill("SwordAirFollowUp", 2005, 5005, "剑·浮空追击", .46f, .10f, .26f, .82f, stun, MakeKnockback("SwordFollow_Knockback", .55f), MakeLaunch("SwordFollow_Launch", 4.2f), SkillAnimationMode.AirAttack);
            var air = MakeMeleeSkill("SwordAirAttack", 2006, 5006, "剑·空中斩", .36f, .08f, .21f, .7f, stun, knockback, null, SkillAnimationMode.AirAttack);
            var dodge = MakeDashSkill("SwordDash", 2007, 5007, "剑·踏步闪", SkillAnimationMode.Dash);
            var counter = MakeDashSkill("SwordCounter", 2008, 5008, "剑·反击", SkillAnimationMode.Attack);
            skills.AddRange(new[] { light, second, finisher, launch, follow, air, dodge, counter });
            timelines.AddRange(TimelinesOf(light, second, finisher, launch, follow, air, dodge, counter));
            return new SkillSet
            {
                Skills = new[] { light, second, finisher, launch, follow, air, dodge, counter },
                Combo = MakeSwordCombo("Combo_Swordsman", light, second, finisher, launch, follow, air, counter),
                Dodge = dodge,
                Launcher = launch,
                AirFollowUp = follow
            };
        }

        static SkillSet BuildFighterSkills(System.Collections.Generic.List<SkillDefinitionAsset> skills, System.Collections.Generic.List<SkillTimelineAsset> timelines, HitStunAsset stun, KnockbackAsset knockback)
        {
            var one = MakeMeleeSkill("Punch1", 3001, 5101, "拳·直拳", .32f, .08f, .18f, .55f, stun, knockback, null, SkillAnimationMode.Attack);
            var two = MakeMeleeSkill("Punch2", 3002, 5102, "拳·身体打击", .38f, .10f, .24f, .62f, stun, knockback, null, SkillAnimationMode.Attack);
            var three = MakeMeleeSkill("Punch3", 3003, 5103, "拳·终结重拳", .52f, .14f, .31f, .9f, stun, MakeKnockback("Punch3_Knockback", .85f), null, SkillAnimationMode.Attack);
            var upper = MakeMeleeSkill("Uppercut", 3004, 5104, "拳·升龙拳", .48f, .12f, .27f, .7f, stun, knockback, MakeLaunch("Uppercut_Launch", 4.8f), SkillAnimationMode.Attack);
            var air = MakeMeleeSkill("AirPunch", 3005, 5105, "拳·空袭", .34f, .08f, .2f, .65f, stun, knockback, null, SkillAnimationMode.AirAttack);
            var dash = MakeMeleeSkill("DashStrike", 3006, 5106, "拳·冲刺拳", .42f, .08f, .24f, .7f, stun, knockback, null, SkillAnimationMode.Dash);
            var slide = MakeMeleeSkill("SlideStrike", 3007, 5107, "拳·滑铲", .44f, .1f, .25f, .68f, stun, knockback, null, SkillAnimationMode.Slide);
            var slam = MakeMeleeSkill("GroundSlam", 3008, 5108, "拳·落地震击", .5f, .08f, .3f, 1.05f, stun, MakeKnockback("GroundSlam_Knockback", .7f), null, SkillAnimationMode.AirAttack);
            skills.AddRange(new[] { one, two, three, upper, air, dash, slide, slam });
            timelines.AddRange(TimelinesOf(one, two, three, upper, air, dash, slide, slam));
            return new SkillSet
            {
                Skills = new[] { one, two, three, upper, air, dash, slide, slam },
                Combo = MakeFighterCombo("Combo_Fighter", one, two, three, upper, air, slam),
                Dodge = dash
            };
        }

        static SkillSet BuildGunSkills(System.Collections.Generic.List<SkillDefinitionAsset> skills, System.Collections.Generic.List<SkillTimelineAsset> timelines, SpawnProjectileAsset spawnFireball, SpawnAoeAsset spawnGround, SpawnProjectileAsset spawnHoming, ApplyDurationAsset applyBurn, HitStunAsset stun, KnockbackAsset knockback)
        {
            spawnGround.UseTargetPoint = true;
            var bullet = Make<ProjectileDefAsset>("PistolBullet");
            bullet.SpecId = 9101; bullet.Speed = 16f; bullet.Lifetime = 1.6f; bullet.HitRadius = .25f; bullet.MaxHits = 1; bullet.SnapshotAtk = true; bullet.SpawnForward = .25f;
            var bulletDamage = Make<DamageEffectAsset>("Damage_PistolBullet"); bulletDamage.Coeff = .7f; bulletDamage.CanCrit = true; bulletDamage.UseSnapshotAtk = true;
            bullet.OnHit = new EffectAsset[] { bulletDamage, stun, knockback };

            var one = MakeProjectileSkill("PistolShot1", 4001, 5201, "枪·单发", bullet, SkillAnimationMode.Shot);
            var two = MakeProjectileSkill("PistolShot2", 4002, 5202, "枪·追射", bullet, SkillAnimationMode.Shot);
            var burst = MakeBurstSkill("PistolBurst", 4003, 5203, "枪·连发", bullet, SkillAnimationMode.Shot);
            var fireball = MakePayloadSkill("FireballShot", 4004, 5204, "枪·火球", new EffectAsset[] { spawnFireball }, SkillAnimationMode.Shot);
            var ground = MakePayloadSkill("GunslingerFireGround", 4005, 5205, "枪·火地", new EffectAsset[] { spawnGround }, SkillAnimationMode.Shot);
            var air = MakeProjectileSkill("AirShot", 4006, 5206, "枪·空中射击", bullet, SkillAnimationMode.AirAttack);
            var dash = MakeDashSkill("DashReload", 4007, 5207, "枪·翻滚", SkillAnimationMode.Dash);
            var slide = MakeProjectileSkill("SlideShot", 4008, 5208, "枪·滑铲射击", bullet, SkillAnimationMode.Slide);
            var homing = MakePayloadSkill("HomingFireball", 4009, 5209, "枪·追踪火球", new EffectAsset[] { spawnHoming }, SkillAnimationMode.Shot);
            skills.AddRange(new[] { one, two, burst, fireball, ground, air, dash, slide, homing });
            timelines.AddRange(TimelinesOf(one, two, burst, fireball, ground, air, dash, slide, homing));
            return new SkillSet
            {
                Skills = new[] { one, two, burst, fireball, ground, air, dash, slide, homing },
                Combo = MakeGunCombo("Combo_Gunslinger", one, two, burst, fireball, ground, air, homing),
                Dodge = dash,
                Fireball = fireball,
                FireGround = ground
            };
        }

        static SkillDefinitionAsset MakeMeleeSkill(
            string name,
            int skillId,
            int timelineId,
            string displayName,
            float duration,
            float hitStart,
            float hitEnd,
            float hitRadius,
            HitStunAsset stun,
            KnockbackAsset knockback,
            LaunchAsset launch,
            SkillAnimationMode animation)
        {
            var damage = Make<DamageEffectAsset>(name + "_Damage");
            damage.Coeff = 1f;
            damage.CanCrit = true;
            damage.UseSnapshotAtk = true;
            damage.HitstopFrames = 2;
            var profile = Make<HitProfileAsset>(name + "_HitProfile");
            profile.Damage = damage;
            profile.Stun = stun;
            profile.Knockback = knockback;
            profile.Launch = launch;
            var timeline = Make<SkillTimelineAsset>("TL_" + name);
            timeline.TimelineIdValue = timelineId;
            timeline.Duration = duration;
            timeline.Clips = new[]
            {
                new TimelineClipAsset { Start = .08f, End = Math.Max(.1f, duration - .1f), Kind = ClipKind.CancelTag },
                new TimelineClipAsset { Start = .04f, End = Math.Min(.25f, duration * .55f), Kind = ClipKind.Move, MoveX = .25f },
                new TimelineClipAsset
                {
                    Start = hitStart,
                    End = hitEnd,
                    Kind = ClipKind.Hitbox,
                    HitRadius = hitRadius,
                    HitOffsetX = hitRadius * .75f,
                    HitProfile = profile
                }
            };
            timeline.Payloads = Array.Empty<TimelinePayloadAsset>();
            return MakeSkill(name, skillId, displayName, timeline, animation);
        }

        static SkillDefinitionAsset MakeProjectileSkill(string name, int skillId, int timelineId, string displayName, ProjectileDefAsset projectile, SkillAnimationMode animation)
        {
            var spawn = Make<SpawnProjectileAsset>("Spawn_" + name);
            spawn.SpecId = projectile.SpecId;
            return MakePayloadSkill(name, skillId, timelineId, displayName, new EffectAsset[] { spawn }, animation);
        }

        static SkillDefinitionAsset MakeBurstSkill(string name, int skillId, int timelineId, string displayName, ProjectileDefAsset projectile, SkillAnimationMode animation)
        {
            var spawn = Make<SpawnProjectileAsset>("Spawn_" + name);
            spawn.SpecId = projectile.SpecId;
            var timeline = Make<SkillTimelineAsset>("TL_" + name);
            timeline.TimelineIdValue = timelineId;
            timeline.Duration = .5f;
            timeline.Clips = new[] { new TimelineClipAsset { Start = 0f, End = .22f, Kind = ClipKind.CancelTag } };
            timeline.Payloads = new[]
            {
                new TimelinePayloadAsset { Time = .06f, Effects = new EffectAsset[] { spawn } },
                new TimelinePayloadAsset { Time = .16f, Effects = new EffectAsset[] { spawn } },
                new TimelinePayloadAsset { Time = .26f, Effects = new EffectAsset[] { spawn } }
            };
            return MakeSkill(name, skillId, displayName, timeline, animation);
        }

        static SkillDefinitionAsset MakePayloadSkill(string name, int skillId, int timelineId, string displayName, EffectAsset[] effects, SkillAnimationMode animation)
        {
            var timeline = Make<SkillTimelineAsset>("TL_" + name);
            timeline.TimelineIdValue = timelineId;
            timeline.Duration = .5f;
            timeline.Clips = new[] { new TimelineClipAsset { Start = 0f, End = .25f, Kind = ClipKind.CancelTag } };
            timeline.Payloads = new[] { new TimelinePayloadAsset { Time = .08f, Effects = effects } };
            return MakeSkill(name, skillId, displayName, timeline, animation);
        }

        static SkillDefinitionAsset MakeDashSkill(string name, int skillId, int timelineId, string displayName, SkillAnimationMode animation)
        {
            var timeline = Make<SkillTimelineAsset>("TL_" + name);
            timeline.TimelineIdValue = timelineId;
            timeline.Duration = .42f;
            timeline.Clips = new[]
            {
                new TimelineClipAsset { Start = 0f, End = .28f, Kind = ClipKind.Move, MoveX = 1.35f },
                new TimelineClipAsset { Start = .04f, End = .22f, Kind = ClipKind.IFrame },
                new TimelineClipAsset { Start = .25f, End = .42f, Kind = ClipKind.CancelTag }
            };
            timeline.Payloads = Array.Empty<TimelinePayloadAsset>();
            return MakeSkill(name, skillId, displayName, timeline, animation);
        }

        static SkillDefinitionAsset MakeSkill(string name, int id, string displayName, SkillTimelineAsset timeline, SkillAnimationMode animation)
        {
            var skill = Make<SkillDefinitionAsset>(name);
            skill.SkillId = id;
            skill.DisplayName = displayName;
            skill.Timeline = timeline;
            skill.AnimationMode = animation;
            skill.Cooldown = 0f;
            skill.CanUseInAir = animation == SkillAnimationMode.AirAttack;
            skill.RequiresTarget = animation != SkillAnimationMode.Dash;
            EditorUtility.SetDirty(skill);
            return skill;
        }

        static KnockbackAsset MakeKnockback(string name, float distance)
        {
            var asset = Make<KnockbackAsset>(name);
            asset.Distance = distance;
            return asset;
        }

        static LaunchAsset MakeLaunch(string name, float speed)
        {
            var asset = Make<LaunchAsset>(name);
            asset.VerticalSpeed = speed;
            return asset;
        }

        static SkillTimelineAsset[] TimelinesOf(params SkillDefinitionAsset[] skills)
        {
            var result = new SkillTimelineAsset[skills.Length];
            for (int i = 0; i < skills.Length; i++) result[i] = skills[i].Timeline;
            return result;
        }

        static ComboTableAsset MakeSwordCombo(string name, SkillDefinitionAsset one, SkillDefinitionAsset two, SkillDefinitionAsset three, SkillDefinitionAsset launcher, SkillDefinitionAsset follow, SkillDefinitionAsset air, SkillDefinitionAsset counter)
        {
            var combo = Make<ComboTableAsset>(name);
            combo.Entries = new[]
            {
                Entry("Attack", one, null, 0),
                Entry("Attack", two, new[] { one }, 10, CommonTags.Cancel.Value),
                Entry("Attack", three, new[] { two }, 10, CommonTags.Cancel.Value),
                Entry("Skill1", launcher, null, 20),
                Entry("Skill1", follow, new[] { launcher }, 30, CommonTags.Cancel.Value),
                Entry("Attack", air, null, 100, CommonTags.Airborne.Value),
                Entry("Skill2", counter, null, 20)
            };
            return combo;
        }

        static ComboTableAsset MakeFighterCombo(string name, SkillDefinitionAsset one, SkillDefinitionAsset two, SkillDefinitionAsset three, SkillDefinitionAsset upper, SkillDefinitionAsset air, SkillDefinitionAsset slam)
        {
            var combo = Make<ComboTableAsset>(name);
            combo.Entries = new[]
            {
                Entry("Attack", one, null, 0),
                Entry("Attack", two, new[] { one }, 10, CommonTags.Cancel.Value),
                Entry("Attack", three, new[] { two }, 10, CommonTags.Cancel.Value),
                Entry("Skill1", upper, null, 20),
                Entry("Attack", air, null, 100, CommonTags.Airborne.Value),
                Entry("Skill2", slam, null, 20)
            };
            return combo;
        }

        static ComboTableAsset MakeGunCombo(string name, SkillDefinitionAsset one, SkillDefinitionAsset two, SkillDefinitionAsset three, SkillDefinitionAsset fireball, SkillDefinitionAsset ground, SkillDefinitionAsset air, SkillDefinitionAsset homing)
        {
            var combo = Make<ComboTableAsset>(name);
            combo.Entries = new[]
            {
                Entry("Attack", one, null, 0),
                Entry("Attack", two, new[] { one }, 10, CommonTags.Cancel.Value),
                Entry("Attack", three, new[] { two }, 10, CommonTags.Cancel.Value),
                Entry("Skill1", fireball, null, 20),
                Entry("Skill1", ground, new[] { fireball }, 30, CommonTags.Cancel.Value),
                Entry("Attack", air, null, 100, CommonTags.Airborne.Value),
                Entry("Skill2", homing, null, 20)
            };
            return combo;
        }

        static ComboEntryAsset Entry(string action, SkillDefinitionAsset skill, SkillDefinitionAsset[] pre, int priority, params int[] tags)
        {
            return new ComboEntryAsset
            {
                InputAction = action,
                Skill = skill,
                PreSkillAssets = pre ?? Array.Empty<SkillDefinitionAsset>(),
                Priority = priority,
                RequiredTags = tags ?? Array.Empty<int>()
            };
        }

        static BtNodeAsset BuildAi(string name, params SkillDefinitionAsset[] attackSkills)
        {
            var dead = Make<CondHasTagAsset>(name + "_Dead"); dead.TagValue = CommonTags.Dead.Value;
            var downed = Make<CondHasTagAsset>(name + "_Downed"); downed.TagValue = CommonTags.Downed.Value;
            var stunned = Make<CondHasTagAsset>(name + "_Stunned"); stunned.TagValue = CommonTags.Stunned.Value;
            var hasTarget = Make<CondHasTargetAsset>(name + "_HasTarget");
            var inRange = Make<CondInRangeAsset>(name + "_InRange");
            var stop = Make<ActStopMoveAsset>(name + "_Stop");
            var face = Make<ActFaceTargetAsset>(name + "_Face");
            var acquire = Make<ActAcquireHostileAsset>(name + "_Acquire");
            var move = Make<ActMoveTowardAsset>(name + "_Move");
            var patrol = Make<ActPatrolAsset>(name + "_Patrol");
            var attackNodes = new BtNodeAsset[4 + attackSkills.Length];
            attackNodes[0] = hasTarget;
            attackNodes[1] = inRange;
            attackNodes[2] = stop;
            attackNodes[3] = face;
            for (int i = 0; i < attackSkills.Length; i++)
            {
                var play = Make<ActPlaySkillAsset>(name + "_Play_" + i);
                play.Skill = attackSkills[i];
                attackNodes[4 + i] = play;
            }
            var attack = Make<BtSequenceAsset>(name + "_Attack"); attack.Children = attackNodes;
            var root = Make<BtSelectorAsset>(name);
            root.Children = new BtNodeAsset[]
            {
                Sequence(name + "_DeadStop", dead, stop),
                Sequence(name + "_DownStop", downed, stop),
                Sequence(name + "_StunStop", stunned, stop),
                attack,
                move,
                acquire,
                patrol
            };
            return root;
        }

        static BtSequenceAsset Sequence(string name, params BtNodeAsset[] children)
        {
            var sequence = Make<BtSequenceAsset>(name);
            sequence.Children = children;
            return sequence;
        }

        static void EnsureInputActions()
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var fullPath = Path.Combine(projectRoot, "Assets", "InputSystem_Actions.inputactions");
            File.WriteAllText(fullPath, InputActionsJson());
            AssetDatabase.ImportAsset("Assets/InputSystem_Actions.inputactions", ImportAssetOptions.ForceSynchronousImport);
        }

        static string InputActionsJson()
        {
            return @"{
  ""name"": ""InputSystem_Actions"",
  ""maps"": [
    {
      ""name"": ""Gameplay"",
      ""id"": ""11111111-1111-4111-8111-111111111111"",
      ""actions"": [
        {""name"": ""Move"", ""type"": ""Value"", ""id"": ""11111111-1111-4111-8111-111111111112"", ""expectedControlType"": ""Vector2""},
        {""name"": ""Attack"", ""type"": ""Button"", ""id"": ""11111111-1111-4111-8111-111111111113""},
        {""name"": ""Skill1"", ""type"": ""Button"", ""id"": ""11111111-1111-4111-8111-111111111114""},
        {""name"": ""Skill2"", ""type"": ""Button"", ""id"": ""11111111-1111-4111-8111-111111111115""},
        {""name"": ""Skill3"", ""type"": ""Button"", ""id"": ""11111111-1111-4111-8111-111111111116""},
        {""name"": ""Dodge"", ""type"": ""Button"", ""id"": ""11111111-1111-4111-8111-111111111117""},
        {""name"": ""Jump"", ""type"": ""Button"", ""id"": ""11111111-1111-4111-8111-111111111118""},
        {""name"": ""Pause"", ""type"": ""Button"", ""id"": ""11111111-1111-4111-8111-111111111119""}
      ],
      ""bindings"": [
        {""name"": ""WASD"", ""id"": ""11111111-1111-4111-8111-111111111120"", ""path"": ""2DVector"", ""action"": ""Move"", ""isComposite"": true},
        {""name"": ""up"", ""id"": ""11111111-1111-4111-8111-111111111121"", ""path"": ""<Keyboard>/w"", ""action"": ""Move"", ""isPartOfComposite"": true},
        {""name"": ""down"", ""id"": ""11111111-1111-4111-8111-111111111122"", ""path"": ""<Keyboard>/s"", ""action"": ""Move"", ""isPartOfComposite"": true},
        {""name"": ""left"", ""id"": ""11111111-1111-4111-8111-111111111123"", ""path"": ""<Keyboard>/a"", ""action"": ""Move"", ""isPartOfComposite"": true},
        {""name"": ""right"", ""id"": ""11111111-1111-4111-8111-111111111124"", ""path"": ""<Keyboard>/d"", ""action"": ""Move"", ""isPartOfComposite"": true},
        {""id"": ""11111111-1111-4111-8111-111111111125"", ""path"": ""<Keyboard>/j"", ""action"": ""Attack""},
        {""id"": ""11111111-1111-4111-8111-111111111126"", ""path"": ""<Keyboard>/k"", ""action"": ""Skill1""},
        {""id"": ""11111111-1111-4111-8111-111111111127"", ""path"": ""<Keyboard>/l"", ""action"": ""Skill2""},
        {""id"": ""11111111-1111-4111-8111-111111111128"", ""path"": ""<Keyboard>/i"", ""action"": ""Skill3""},
        {""id"": ""11111111-1111-4111-8111-111111111129"", ""path"": ""<Keyboard>/leftShift"", ""action"": ""Dodge""},
        {""id"": ""11111111-1111-4111-8111-111111111130"", ""path"": ""<Keyboard>/space"", ""action"": ""Jump""},
        {""id"": ""11111111-1111-4111-8111-111111111131"", ""path"": ""<Keyboard>/escape"", ""action"": ""Pause""}
      ]
    },
    {
      ""name"": ""Debug"",
      ""id"": ""22222222-2222-4222-8222-222222222222"",
      ""actions"": [
        {""name"": ""Hitbox"", ""type"": ""Button"", ""id"": ""22222222-2222-4222-8222-222222222223""},
        {""name"": ""ToggleEnemyAI"", ""type"": ""Button"", ""id"": ""22222222-2222-4222-8222-222222222225""}
      ],
      ""bindings"": [
        {""id"": ""22222222-2222-4222-8222-222222222224"", ""path"": ""<Keyboard>/f3"", ""action"": ""Hitbox""},
        {""id"": ""22222222-2222-4222-8222-222222222226"", ""path"": ""<Keyboard>/f4"", ""action"": ""ToggleEnemyAI""}
      ]
    }
  ],
  ""controlSchemes"": []
}";
        }

        static void EnsureArenaSpawns()
        {
            var spawns = Make<ArenaSpawnTableSO>("ArenaSpawns");
            spawns.Entries = new[]
            {
                new SpawnEntryAsset { BlueprintId = "swordsman", IsLocalPlayer = true },
                new SpawnEntryAsset { BlueprintId = "melee_ai", Position = new Vector3(2.2f, 0f, 0f), YawDegrees = 180f, CountsForWin = true },
                new SpawnEntryAsset { BlueprintId = "melee_guard", Position = new Vector3(3.8f, 0f, 1.6f), YawDegrees = 180f, CountsForWin = true },
                new SpawnEntryAsset { BlueprintId = "ranged_ai", Position = new Vector3(4.2f, 0f, -2f), YawDegrees = 180f, CountsForWin = true },
                new SpawnEntryAsset { BlueprintId = "stake", Position = new Vector3(.55f, 0f, 0f) },
                new SpawnEntryAsset { BlueprintId = "stake", Position = new Vector3(3f, 0f, 2.2f) }
            };
            EditorUtility.SetDirty(spawns);
        }

        static void BindRuntimeScenes(CombatDatabaseAsset database)
        {
            var spawns = AssetDatabase.LoadAssetAtPath<ArenaSpawnTableSO>(GeneratedPath<ArenaSpawnTableSO>("ArenaSpawns"));
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/InputSystem_Actions.inputactions");
            var views = AssetDatabase.LoadAssetAtPath<ViewPrefabTable>("Assets/Resources/Stickman/StickmanViews.asset");
            var scenes = new[] { "Assets/Scenes/Boot.unity", "Assets/Scenes/Arena.unity" };
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!File.Exists(Path.Combine(Directory.GetParent(Application.dataPath).FullName, scenes[i])))
                    continue;
                var scene = EditorSceneManager.OpenScene(scenes[i], OpenSceneMode.Single);
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<ArenaBootstrap>();
                if (bootstrap == null)
                    continue;
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("Database").objectReferenceValue = database;
                serialized.FindProperty("Spawns").objectReferenceValue = spawns;
                serialized.FindProperty("Actions").objectReferenceValue = actions;
                if (views != null)
                    serialized.FindProperty("Views").objectReferenceValue = views;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }
        }

        static T Make<T>(string name) where T : ScriptableObject
        {
            string path = GeneratedPath<T>(name);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.SetDirty(existing);
                return existing;
            }
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static void MigrateLegacyAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureGeneratedFolders();

            var absoluteRoot = ProjectFilePath(Root);
            if (!Directory.Exists(absoluteRoot))
                return;

            var files = Directory.GetFiles(absoluteRoot, "*.asset", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                var source = AssetPath(files[i]);
                var asset = AssetDatabase.LoadMainAssetAtPath(source);
                if (asset == null)
                    continue;

                var name = Path.GetFileNameWithoutExtension(files[i]);
                var target = GeneratedPath(asset.GetType(), name);
                if (string.Equals(source, target, StringComparison.Ordinal))
                    continue;
                if (AssetDatabase.LoadMainAssetAtPath(target) != null)
                    throw new InvalidOperationException("Cannot move generated asset because the destination already exists: " + target);

                var error = AssetDatabase.MoveAsset(source, target);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException("Failed to move generated asset " + source + ": " + error);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void EnsureGeneratedFolders()
        {
            var folders = new[]
            {
                DatabaseFolder, CharactersFolder, SkillsFolder, TimelinesFolder,
                CombosFolder, BehaviorTreesFolder, EffectsFolder, HitProfilesFolder,
                ProjectilesFolder, AoesFolder, SummonsFolder, CuesFolder, MotorFolder,
                SpawnsFolder, MiscFolder
            };
            for (int i = 0; i < folders.Length; i++)
            {
                var path = Root + "/" + folders[i];
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder(Root, folders[i]);
            }
        }

        static string GeneratedPath<T>(string name) where T : ScriptableObject
            => GeneratedPath(typeof(T), name);

        static string GeneratedPath(Type type, string name)
            => Root + "/" + FolderFor(type) + "/" + name + ".asset";

        static string FolderFor(Type type)
        {
            if (type == typeof(CombatDatabaseAsset)) return DatabaseFolder;
            if (type == typeof(CharacterDefinitionAsset)) return CharactersFolder;
            if (type == typeof(SkillDefinitionAsset)) return SkillsFolder;
            if (type == typeof(SkillTimelineAsset)) return TimelinesFolder;
            if (type == typeof(ComboTableAsset)) return CombosFolder;
            if (typeof(BtNodeAsset).IsAssignableFrom(type)) return BehaviorTreesFolder;
            if (type == typeof(HitProfileAsset)) return HitProfilesFolder;
            if (type == typeof(ProjectileDefAsset)) return ProjectilesFolder;
            if (type == typeof(AoeDefAsset)) return AoesFolder;
            if (type == typeof(SummonDefAsset)) return SummonsFolder;
            if (type == typeof(CueLibraryAsset) || type == typeof(PlayCueAsset)) return CuesFolder;
            if (type == typeof(CharacterMotorAsset)) return MotorFolder;
            if (type == typeof(ArenaSpawnTableSO) ||
                type == typeof(SpawnProjectileAsset) ||
                type == typeof(SpawnAoeAsset) ||
                type == typeof(SpawnSummonAsset)) return SpawnsFolder;
            if (typeof(EffectAsset).IsAssignableFrom(type) || type == typeof(DurationSpecAsset)) return EffectsFolder;
            return MiscFolder;
        }

        static string ProjectFilePath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static string AssetPath(string absolutePath)
        {
            var assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullPath = Path.GetFullPath(absolutePath);
            return "Assets" + fullPath.Substring(assetsRoot.Length).Replace(Path.DirectorySeparatorChar, '/');
        }
    }

    public static class ValidateMenu
    {
        [MenuItem("Combat/Validate Database")]
        public static void Validate()
        {
            var database = AssetDatabase.LoadAssetAtPath<CombatDatabaseAsset>(GenerateDefaultDatabase.DatabaseAssetPath);
            if (database == null) throw new InvalidOperationException("Generate database first");
            var data = database.BakeAll();
            var report = CombatValidator.Validate(data);
            if (report.HasError) throw new InvalidOperationException(report.ToString());
            if (data.Skills == null || data.Skills.Count == 0)
                throw new InvalidOperationException("No generated skills.");
            if (data.Characters == null || data.Characters.Count == 0)
                throw new InvalidOperationException("No generated characters.");
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
