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
