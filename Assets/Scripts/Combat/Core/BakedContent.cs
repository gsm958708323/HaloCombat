using System;

namespace Combat.Core
{
    public struct SpawnEntry
    {
        public string BlueprintId;
        public SimVec3 Position;
        public float YawDegrees;
        public bool IsLocalPlayer;
        public bool CountsForWin;
        public float LeashOverride;
        public float PatrolOverride;
    }

    public sealed class SpawnTable
    {
        public SpawnEntry[] Entries = System.Array.Empty<SpawnEntry>();
    }

    public sealed class BakedDatabase
    {
        public TimelineLibrary Timelines = new TimelineLibrary();
        public ComboTableSO Combos = new ComboTableSO();
        public ProjectileCatalog Projectiles = new ProjectileCatalog();
        public AoeCatalog Aoes = new AoeCatalog();
        public SummonCatalog Summons = new SummonCatalog();
        public CueLibrary Cues = new CueLibrary();
        public DurationSpec Burn;
        public DurationSpec AuraSlow;
        public SpawnTable Spawns = new SpawnTable();
    }
    public sealed class BakedCombatData
    {
        public ComboTableSO Combo;
        public TimelineLibrary Timelines;
        public SkillCatalog Skills;
        public CharacterCatalog Characters;
        public ProjectileCatalog Projectiles;
        public AoeCatalog Aoes;
        public SummonCatalog Summons;
        public CueLibrary Cues;
        public MotorConfig Motor;
        public int ContentSerial;

        public void Install(CombatWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            world.ReplaceCatalogs(Projectiles, Aoes, Summons);
            world.ReplaceCues(Cues);
        }
    }

    /// <summary>Runtime definition of a skill. The timeline is resolved by SkillCatalog.</summary>
    public sealed class SkillDefinition
    {
        public SkillNodeId Id;
        public TimelineId Timeline;
        public string DisplayName;
        public SkillAnimationMode AnimationMode;
    }

    public sealed class SkillCatalog
    {
        readonly System.Collections.Generic.Dictionary<int, SkillDefinition> _map =
            new System.Collections.Generic.Dictionary<int, SkillDefinition>(32);

        public int Count => _map.Count;

        public void Register(SkillDefinition definition)
        {
            if (definition == null || !definition.Id.IsValid || !definition.Timeline.IsValid)
                throw new ArgumentException("Invalid skill definition", nameof(definition));
            if (_map.ContainsKey(definition.Id.Value))
                throw new InvalidOperationException("Duplicate skill id " + definition.Id.Value);
            _map.Add(definition.Id.Value, definition);
        }

        public bool TryGet(SkillNodeId id, out SkillDefinition definition)
            => _map.TryGetValue(id.Value, out definition);

        public SkillDefinition Require(SkillNodeId id)
        {
            if (!TryGet(id, out var definition))
                throw new InvalidOperationException("Missing skill " + id.Value);
            return definition;
        }
    }

    public sealed class PlayerCombatConfig
    {
        public InputToken JumpInput = InputToken.Jump;
        public InputToken DodgeInput = Season2Tokens.Dodge;
        public SkillNodeId DodgeSkill = SkillNodeId.None;

        public bool HasDodge => DodgeSkill.IsValid;
    }

    public sealed class CharacterDefinition
    {
        public string BlueprintId;
        public string DisplayName;
        public bool IsPlayer;
        public bool PlayerSelectable;
        public SkillDefinition[] Skills = Array.Empty<SkillDefinition>();
        public ComboTableSO Combo;
        public BtNode BehaviorTree;
        public PlayerCombatConfig PlayerCombat;
        public float AcquireRadius = 8f;
        public float AttackRange = 1.15f;
        public float FollowRange = 1.5f;
        public float LeashRange = 20f;
        public float PatrolRadius;
    }

    public sealed class CharacterCatalog
    {
        readonly System.Collections.Generic.Dictionary<string, CharacterDefinition> _map =
            new System.Collections.Generic.Dictionary<string, CharacterDefinition>(StringComparer.Ordinal);

        public int Count => _map.Count;

        public void Register(CharacterDefinition definition)
        {
            if (definition == null || string.IsNullOrEmpty(definition.BlueprintId))
                throw new ArgumentException("Invalid character definition", nameof(definition));
            if (_map.ContainsKey(definition.BlueprintId))
                throw new InvalidOperationException("Duplicate character " + definition.BlueprintId);
            _map.Add(definition.BlueprintId, definition);
        }

        public bool TryGet(string blueprintId, out CharacterDefinition definition)
            => _map.TryGetValue(blueprintId ?? string.Empty, out definition);

        public CharacterDefinition Require(string blueprintId)
        {
            if (!TryGet(blueprintId, out var definition))
                throw new InvalidOperationException("Missing character " + blueprintId);
            return definition;
        }

        public System.Collections.Generic.List<CharacterDefinition> PlayerOptions()
        {
            var result = new System.Collections.Generic.List<CharacterDefinition>();
            foreach (var pair in _map)
                if (pair.Value.IsPlayer && pair.Value.PlayerSelectable)
                    result.Add(pair.Value);
            result.Sort((a, b) => string.CompareOrdinal(a.BlueprintId, b.BlueprintId));
            return result;
        }
    }

    public interface ICombatContent
    {
        BakedCombatData Bake();
    }

    public sealed class CodeCombatContent : ICombatContent
    {
        public BakedCombatData Bake()
        {
            DemoTables.ResetG1MeleeDefaults();
            var data = new BakedCombatData
            {
                Combo = DemoTables.G1G2(),
                Timelines = DemoTables.MakeLib(),
                Projectiles = new ProjectileCatalog(),
                Aoes = new AoeCatalog(),
                Summons = new SummonCatalog(),
                Cues = CueLibrary.DefaultCombat(),
                Motor = MotorConfig.SeasonOneDefaults(),
                ContentSerial = Environment.TickCount
            };
            CombatCatalog.RegisterDefaults(
                data.Projectiles, data.Aoes, CombatCatalog.Burn(), data.Summons);
            return data;
        }
    }
}
