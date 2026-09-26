namespace Combat.Core
{
    /// <summary>
    /// Per-blueprint runtime tuning for the Buff Arena: team, body radius, ammo and the base
    /// attribute values. Baked from BuffArenaActorDefAsset, so the runtime never reads these
    /// from literals.
    /// </summary>
    public sealed class BuffArenaActorDef
    {
        public string BlueprintId = string.Empty;
        public string ViewBlueprintId = string.Empty;
        public int TeamId;
        public float BodyRadius = .25f;
        public int AmmoCapacity;

        // The player takes HP from this blueprint. MaxHpPerIndex is used for enemy scaling.
        public float MaxHp = 100f;
        public float MaxHpPerIndex;

        public float Atk = 10f;
        public float AtkRandomRange;
        public float AtkPerIndex;

        public float MoveSpeed = 5f;
        public float ActionSpeed = 1f;
        public float CritRate;

        // Legacy source-engine speed conversion, kept as parameters so the exact
        // pre-migration numbers survive. See BuffArenaSession.ResolveMoveSpeed.
        public bool UseLegacySpeedCurve;
        public float LegacySpeedBase = 50f;
        public float LegacySpeedRandomRange = 20f;
        public float MoveSpeedCurveScale = 5.6f;
        public float MoveSpeedCurveDivisor = 100f;
        public float MoveSpeedCurveOffset = .2f;
    }
}
