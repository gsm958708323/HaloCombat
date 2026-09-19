using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Buff Arena Database")]
    public sealed class BuffArenaDatabaseAsset : ScriptableObject
    {
        public string[] SkillIds =
        {
            "fire", "roll", "spaceMonkeyBall", "homingMissle", "cloakBoomerang",
            "teleportBullet", "grenade", "explosiveBarrel", "reload"
        };

        public string[] ProjectileIds =
        {
            "normal0", "normal1", "cloakBoomerang", "teleportBullet", "boomball"
        };

        public string[] AoeIds =
        {
            "BulletShield", "SpaceMonkeyBall", "BlackHole", "BoomExplosive", "StayingBoom"
        };

        public int PlayerMaxHp = 500;
        public int PlayerAmmoCapacity = 60;
        public int MaxEnemies = 10;
        public float SpawnPeriod = 10f;
        public float EnemyCleanupDelay = 5f;
        public float BarrelSelfDamagePeriod = 5f;
        public int Seed = 1;
        public CharacterMotorAsset Motor;

        [Header("Presentation")]
        [Tooltip("Framing used when the Arena scene has to build its own camera rig at runtime.")]
        public float CameraDistance = 2.5f;
        public float CameraHeight = 2.5f;
        public float CameraBaseFov = 60f;

        [Header("Content (Generated)")]
        [Tooltip("Index-aligned with ProjectileIds. When fully populated this replaces the code-defined projectiles.")]
        public ProjectileDefAsset[] Projectiles;
        [Tooltip("Index-aligned with AoeIds.")]
        public AoeDefAsset[] Aoes;
        [Tooltip("Registered in order; ids come from each asset.")]
        public SkillTimelineAsset[] Timelines;
        public BuffArenaSkillAsset[] Skills;
        public BuffArenaActorDefAsset[] Actors;
        public CueLibraryAsset Cues;

        [Header("Switches")]
        [Tooltip("Master switch: when off the runtime uses the code-defined table instead.")]
        public bool UseGeneratedContent;
        [Tooltip("SoStrict refuses to start when the generated content is unusable; SoWithWarnings " +
                 "falls back to the code table and reports an error. SoStrict is the target state.")]
        public ContentSourcePolicy Policy = ContentSourcePolicy.SoWithWarnings;

        public BuffArenaSourceConfig Bake()
        {
            return new BuffArenaSourceConfig
            {
                SkillIds = SkillIds,
                ProjectileIds = ProjectileIds,
                AoeIds = AoeIds,
                PlayerMaxHp = PlayerMaxHp,
                PlayerAmmoCapacity = PlayerAmmoCapacity,
                MaxEnemies = MaxEnemies,
                SpawnPeriod = SpawnPeriod,
                EnemyCleanupDelay = EnemyCleanupDelay,
                BarrelSelfDamagePeriod = BarrelSelfDamagePeriod,
                Seed = Seed,
                Motor = Motor != null ? Motor.Bake() : MotorConfig.SeasonOneDefaults(),
                Actors = BakeActors(),
                Policy = Policy,
                Content = BakeContent(),
                ContentError = LastContentError
            };
        }

        /// Builds the runtime database straight from the assigned assets. Returns null when
        /// the content arrays are not fully populated, so callers can fall back to the
        /// code-defined table (used by the pure C# regression suite).
        /// Explains why the last BakeContent() call returned null, so the runtime fallback
        /// message can say what is actually missing.
        public string LastContentError { get; private set; }

        public BuffArenaData BakeContent()
        {
            LastContentError = null;
            if (!UseGeneratedContent)
            {
                LastContentError = "Use Generated Content is off";
                return null;
            }
            if (!HasCompleteContent())
            {
                LastContentError = IncompleteReason;
                return null;
            }
            return BakeContentCore();
        }

        const string IncompleteReason =
            "the generated assets are incomplete: projectiles, aoes, timelines, skills, "
            + "actor definitions and a motor asset all have to be present and non-empty";

        /// <summary>
        /// Bakes the generated content without consulting UseGeneratedContent, so editor tooling
        /// can verify the assets while the runtime still runs on the code table. Returns null
        /// (with LastContentError set) when the content is unusable.
        /// </summary>
        public BuffArenaData BakeContentForVerification()
        {
            LastContentError = null;
            if (!HasCompleteContent())
            {
                LastContentError = IncompleteReason;
                return null;
            }
            return BakeContentCore();
        }

        BuffArenaData BakeContentCore()
        {
            var data = new BuffArenaData();
            for (int i = 0; i < Timelines.Length; i++)
                data.Timelines.Register(Timelines[i].Bake());
            data.Projectiles.Register(Projectiles[0].Bake());
            for (int i = 1; i < Projectiles.Length; i++)
                data.Projectiles.Register(Projectiles[i].Bake());
            data.Aoes.Register(Aoes[0].Bake());
            for (int i = 1; i < Aoes.Length; i++)
                data.Aoes.Register(Aoes[i].Bake());
            if (Cues != null)
                data.Cues = Cues.Bake();
            for (int i = 0; i < Skills.Length; i++)
                data.Skills.Add(Skills[i].Bake());
            var actorDefs = BakeActors();
            for (int i = 0; i < actorDefs.Length; i++)
                if (actorDefs[i] != null) data.Actors.Add(actorDefs[i]);

            if (HasCastlessPayload(data))
            {
                LastContentError = "generated timelines hold payload slots whose effect failed to bake";
                Debug.LogError(
                    "BuffArenaDatabase: " + LastContentError
                        + ". Serving them would ship castless skills, so the code-defined table stays "
                        + "authoritative. Rebuild the content assets, or turn Use Generated Content off.",
                    this
                );
                return null;
            }
            return data;
        }

        /// <summary>
        /// True when any timeline payload holds a slot whose IEffect failed to bake. The builder
        /// resolves effect assets by name, so a naming mismatch yields null slots — a skill that
        /// casts but spawns nothing.
        /// </summary>
        static bool HasCastlessPayload(BuffArenaData data)
        {
            foreach (var timeline in data.Timelines.All)
            {
                var payloads = timeline.Payloads;
                if (payloads == null)
                    continue;
                for (int i = 0; i < payloads.Length; i++)
                {
                    var effects = payloads[i].Effects;
                    if (effects == null)
                        continue;
                    for (int j = 0; j < effects.Length; j++)
                        if (effects[j] == null)
                            return true;
                }
            }
            return false;
        }

        /// True when every definition the runtime needs is present. The id lists are the
        /// source-game names kept for validation; the asset arrays carry the actual specs,
        /// so the counts only have to cover the ids that have a spec.
        public bool HasCompleteContent()
        {
            if (Projectiles == null || Projectiles.Length < ProjectileIds.Length) return false;
            if (Aoes == null || Aoes.Length < AoeIds.Length) return false;
            if (Timelines == null || Timelines.Length == 0) return false;
            if (Skills == null || Skills.Length == 0) return false;
            if (Actors == null || Actors.Length == 0) return false;
            if (Motor == null) return false;
            for (int i = 0; i < Timelines.Length; i++)
                if (Timelines[i] == null) return false;
            for (int i = 0; i < Skills.Length; i++)
                if (Skills[i] == null) return false;
            for (int i = 0; i < Actors.Length; i++)
                if (Actors[i] == null || string.IsNullOrEmpty(Actors[i].BlueprintId)) return false;
            return true;
        }

        /// <summary>Baked per-blueprint actor definitions. Shared by the code path (which
        /// registers the same shape) and the asset path.</summary>
        public BuffArenaActorDef[] BakeActors()
        {
            if (Actors == null) return Array.Empty<BuffArenaActorDef>();
            var result = new BuffArenaActorDef[Actors.Length];
            for (int i = 0; i < Actors.Length; i++)
                result[i] = Actors[i] != null ? Actors[i].Bake() : null;
            return result;
        }

        void OnValidate()
        {
            if (Projectiles != null && Projectiles.Length > 0 && Projectiles.Length < ProjectileIds.Length)
                Debug.LogError("BuffArenaDatabase: fewer projectiles than ProjectileIds.", this);
            if (Aoes != null && Aoes.Length > 0 && Aoes.Length < AoeIds.Length)
                Debug.LogError("BuffArenaDatabase: fewer AoEs than AoeIds.", this);
        }
    }
}
