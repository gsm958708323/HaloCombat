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

        [Header("Content (Generated)")]
        [Tooltip("Index-aligned with ProjectileIds. When fully populated this replaces the code-defined projectiles.")]
        public ProjectileDefAsset[] Projectiles;
        [Tooltip("Index-aligned with AoeIds.")]
        public AoeDefAsset[] Aoes;
        [Tooltip("Registered in order; ids come from each asset.")]
        public SkillTimelineAsset[] Timelines;
        public BuffArenaSkillAsset[] Skills;
        public CueLibraryAsset Cues;

        [Header("Switches")]
        [Tooltip("Off while the generated assets are still missing timeline payloads/clips.")]
        public bool UseGeneratedContent;

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
                Content = BakeContent()
            };
        }

        /// Builds the runtime database straight from the assigned assets. Returns null when
        /// the content arrays are not fully populated, so callers can fall back to the
        /// code-defined table (used by the pure C# regression suite).
        public BuffArenaData BakeContent()
        {
            // The generated timeline assets do not carry working payload effects yet (the
            // builder resolves effect assets by name and most asset class names do not match),
            // so serving them would ship castless skills. Until that is fixed the code table
            // stays authoritative at runtime; flip this on once the equivalence check reports
            // zero diffs.
            if (!UseGeneratedContent) return null;
            if (!HasCompleteContent()) return null;

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

            if (HasCastlessPayload(data))
            {
                Debug.LogError(
                    "BuffArenaDatabase: generated timelines hold payload slots whose effect failed to bake. "
                        + "Serving them would ship castless skills, so the code-defined table stays authoritative. "
                        + "Fix the builder's effect-asset resolution, or turn Use Generated Content off.",
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
            return Projectiles != null && Projectiles.Length >= ProjectileIds.Length &&
                   Aoes != null && Aoes.Length >= AoeIds.Length &&
                   Timelines != null && Timelines.Length > 0 &&
                   Skills != null && Skills.Length > 0;
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
