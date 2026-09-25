using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Buff Arena Database")]
    public sealed class BuffArenaDatabaseAsset : ScriptableObject
    {
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

        [Header("Content")]
        [Tooltip("Every definition the Arena runs on. Authored here; the runtime has no code fallback.")]
        public ProjectileDefAsset[] Projectiles;
        [Tooltip("AoE definitions registered by SpecId.")]
        public AoeDefAsset[] Aoes;
        [Tooltip("Registered in order; ids come from each asset.")]
        public SkillTimelineAsset[] Timelines;
        public BuffArenaSkillAsset[] Skills;
        public BuffArenaActorDefAsset[] Actors;
        public CueLibraryAsset Cues;

        /// Explains why the last Bake() call returned null, so startup can say what is wrong
        /// instead of falling back to something else.
        public string LastContentError { get; private set; }

        const string IncompleteReason =
            "the authored content is incomplete: projectiles, aoes, timelines, skills, actors "
            + "and a motor asset all have to be present and non-empty";

        /// <summary>
        /// Builds the runtime database from the authored assets. Returns null (with
        /// LastContentError set) when the content is unusable; there is no code-defined
        /// fallback, so callers must fail the session.
        /// </summary>
        public BuffArenaData Bake()
        {
            LastContentError = null;
            if (!HasCompleteContent())
            {
                LastContentError = IncompleteReason;
                return null;
            }

            var data = BakeCore();
            if (data == null) return null;

            if (!ValidateReferences(data, out string error))
            {
                LastContentError = error;
                Debug.LogError("BuffArenaDatabase: " + error, this);
                return null;
            }

            return data;
        }

        BuffArenaData BakeCore()
        {
            var data = new BuffArenaData
            {
                PlayerMaxHp = PlayerMaxHp,
                PlayerAmmoCapacity = PlayerAmmoCapacity,
                MaxEnemies = MaxEnemies,
                SpawnPeriod = SpawnPeriod,
                EnemyCleanupDelay = EnemyCleanupDelay,
                BarrelSelfDamagePeriod = BarrelSelfDamagePeriod,
                Seed = Seed,
                Motor = Motor != null ? Motor.Bake() : MotorConfig.SeasonOneDefaults()
            };

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
                LastContentError = "authored timelines hold payload slots whose effect failed to bake";
                Debug.LogError(
                    "BuffArenaDatabase: " + LastContentError
                        + ". Serving them would ship castless skills, so the session refuses to start. "
                        + "Re-author the content assets.",
                    this
                );
                return null;
            }
            return data;
        }

        /// <summary>
        /// True when any timeline payload holds a slot whose IEffect failed to bake. Effect
        /// assets resolve through BakeNew(), so a broken sub-asset reference yields null slots
        /// — a skill that casts but spawns nothing.
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

        /// <summary>
        /// Cross-references that used to be guaranteed by generating the assets from a code
        /// table: every binding points at a real skill, every skill points at a real timeline,
        /// and the blueprints the factory asks for by name exist.
        /// </summary>
        static bool ValidateReferences(BuffArenaData data, out string error)
        {
            string[] requiredBlueprints =
            {
                BuffArenaIds.PlayerBlueprint, BuffArenaIds.EnemyBlueprint, BuffArenaIds.BarrelBlueprint
            };
            for (int i = 0; i < requiredBlueprints.Length; i++)
            {
                bool found = false;
                for (int k = 0; k < data.Actors.Count; k++)
                {
                    if (!string.Equals(data.Actors[k].BlueprintId, requiredBlueprints[i], StringComparison.Ordinal))
                        continue;
                    found = true;
                    break;
                }
                if (found) continue;
                error = "the actor table has no '" + requiredBlueprints[i] + "' blueprint";
                return false;
            }

            for (int i = 0; i < data.Skills.Count; i++)
            {
                var skill = data.Skills[i];
                if (!data.Timelines.TryGet(skill.Timeline, out _))
                {
                    error = "skill " + skill.Id.Value + " points at missing timeline " + skill.Timeline.Value;
                    return false;
                }
                if (skill.FallbackSkill.IsValid && !data.TryGetSkill(skill.FallbackSkill, out _))
                {
                    error = "skill " + skill.Id.Value + " falls back to missing skill " + skill.FallbackSkill.Value;
                    return false;
                }
            }

            if (data.PlayerMaxHp <= 0 || data.PlayerAmmoCapacity <= 0 || data.MaxEnemies <= 0 ||
                data.SpawnPeriod <= 0f || data.EnemyCleanupDelay <= 0f || data.BarrelSelfDamagePeriod <= 0f)
            {
                error = "runtime settings contain a non-positive value";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>True when every definition the runtime needs is present and non-null.</summary>
        public bool HasCompleteContent()
        {
            if (!HasEntries(Projectiles) || !HasEntries(Aoes) || !HasEntries(Timelines) ||
                !HasEntries(Skills) || !HasEntries(Actors))
                return false;
            return Motor != null;
        }

        static bool HasEntries<T>(T[] items) where T : UnityEngine.Object
        {
            if (items == null || items.Length == 0) return false;
            for (int i = 0; i < items.Length; i++)
                if (items[i] == null) return false;
            return true;
        }

        /// <summary>Baked per-blueprint actor definitions.</summary>
        BuffArenaActorDef[] BakeActors()
        {
            if (Actors == null) return Array.Empty<BuffArenaActorDef>();
            var result = new BuffArenaActorDef[Actors.Length];
            for (int i = 0; i < Actors.Length; i++)
                result[i] = Actors[i] != null ? Actors[i].Bake() : null;
            return result;
        }

        void OnValidate()
        {
            // Surface the obvious authoring mistakes in the Inspector instead of at play time.
            if (Motor == null) Debug.LogError("BuffArenaDatabase: no motor asset assigned.", this);
        }
    }
}
