using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    /// <summary>Inspector-facing form of BuffArenaActorDef: team, body radius, ammo and the
    /// base attribute values for one blueprint.</summary>
    [CreateAssetMenu(menuName = "Combat/Buff Arena Actor")]
    public sealed class BuffArenaActorDefAsset : ScriptableObject
    {
        public string BlueprintId;
        public string ViewBlueprintId;
        public bool IsPlayer;
        public int TeamId;
        public float BodyRadius = .25f;
        public int AmmoCapacity;

        public float MaxHp = 100f;
        public float MaxHpPerIndex;
        public float Atk = 10f;
        public float AtkRandomRange;
        public float AtkPerIndex;
        public float MoveSpeed = 5f;
        public float ActionSpeed = 1f;
        public float CritRate;

        public bool UseLegacySpeedCurve;
        public float LegacySpeedBase = 50f;
        public float LegacySpeedRandomRange = 20f;
        public float MoveSpeedCurveScale = 5.6f;
        public float MoveSpeedCurveDivisor = 100f;
        public float MoveSpeedCurveOffset = .2f;

        public BuffArenaActorDef Bake()
        {
            if (string.IsNullOrEmpty(BlueprintId))
                throw new InvalidOperationException("BuffArenaActorDefAsset has no BlueprintId: " + name);
            return new BuffArenaActorDef
            {
                BlueprintId = BlueprintId,
                ViewBlueprintId = ViewBlueprintId,
                IsPlayer = IsPlayer,
                TeamId = TeamId,
                BodyRadius = BodyRadius,
                AmmoCapacity = AmmoCapacity,
                MaxHp = MaxHp,
                MaxHpPerIndex = MaxHpPerIndex,
                Atk = Atk,
                AtkRandomRange = AtkRandomRange,
                AtkPerIndex = AtkPerIndex,
                MoveSpeed = MoveSpeed,
                ActionSpeed = ActionSpeed,
                CritRate = CritRate,
                UseLegacySpeedCurve = UseLegacySpeedCurve,
                LegacySpeedBase = LegacySpeedBase,
                LegacySpeedRandomRange = LegacySpeedRandomRange,
                MoveSpeedCurveScale = MoveSpeedCurveScale,
                MoveSpeedCurveDivisor = MoveSpeedCurveDivisor,
                MoveSpeedCurveOffset = MoveSpeedCurveOffset
            };
        }
    }
}
