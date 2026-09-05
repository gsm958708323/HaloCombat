using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Arena Spawn Table")]
    public sealed class ArenaSpawnTableSO : ScriptableObject
    {
        public SpawnEntryAsset[] Entries;

        public SpawnTable Bake()
        {
            var t = new SpawnTable();
            if (Entries == null)
            {
                t.Entries = System.Array.Empty<SpawnEntry>();
                return t;
            }
            t.Entries = new SpawnEntry[Entries.Length];
            for (int i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                t.Entries[i] = new SpawnEntry
                {
                    BlueprintId = e.BlueprintId,
                    Position = new SimVec3(e.Position.x, e.Position.y, e.Position.z),
                    YawDegrees = e.YawDegrees,
                    IsLocalPlayer = e.IsLocalPlayer,
                    CountsForWin = e.CountsForWin,
                    LeashOverride = e.LeashOverride,
                    PatrolOverride = e.PatrolOverride,
                };
            }
            return t;
        }
    }

    [System.Serializable]
    public struct SpawnEntryAsset
    {
        public string BlueprintId;
        public Vector3 Position;
        public float YawDegrees;
        public bool IsLocalPlayer,
            CountsForWin;
        public float LeashOverride,
            PatrolOverride;
    }
}
