using System;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    [CreateAssetMenu(menuName = "Combat/View Prefab Table")]
    public sealed class ViewPrefabTable : ScriptableObject
    {
        public ViewPrefabEntry[] Entries;

        void OnValidate()
        {
            if (Entries == null) return;
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Entries.Length; i++)
            {
                var entry = Entries[i];
                if (string.IsNullOrEmpty(entry.BlueprintId))
                {
                    Debug.LogError("ViewPrefabTable contains an empty BlueprintId.", this);
                    continue;
                }
                if (!keys.Add(entry.BlueprintId))
                    Debug.LogError("Duplicate view BlueprintId: " + entry.BlueprintId, this);
                if (entry.Kind == ViewKind.Character && entry.Prefab == null)
                    Debug.LogError("Character view requires a Prefab: " + entry.BlueprintId, this);
                if ((entry.Features & (ViewFeatures.Animation | ViewFeatures.BuffFx | ViewFeatures.Camera |
                                      ViewFeatures.Feedback | ViewFeatures.Hud | ViewFeatures.HealthRing)) != 0 &&
                    entry.Kind != ViewKind.Character)
                    Debug.LogError("Character-only view features are invalid for: " + entry.BlueprintId, this);
            }
        }

        public bool TryGet(string id, out ViewPrefabEntry entry)
        {
            if (Entries != null)
            {
                for (int i = 0; i < Entries.Length; i++)
                {
                    if (string.Equals(Entries[i].BlueprintId, id, StringComparison.Ordinal))
                    {
                        entry = Entries[i];
                        return true;
                    }
                }
            }

            entry = default(ViewPrefabEntry);
            return false;
        }
    }

    public enum ViewKind : byte
    {
        None,
        Character,
        RuntimeBody
    }

    [Flags]
    public enum ViewFeatures : byte
    {
        None = 0,
        Pose = 1 << 0,
        Animation = 1 << 1,
        BuffFx = 1 << 2,
        Camera = 1 << 3,
        Feedback = 1 << 4,
        Hud = 1 << 5,
        HitboxGizmo = 1 << 6,
        HealthRing = 1 << 7
    }

    public enum ViewAnimationProfile : byte
    {
        Generic,
        Gunner
    }

    [System.Serializable]
    public struct ViewPrefabEntry
    {
        public string BlueprintId;
        public GameObject Prefab;
        public ViewKind Kind;
        public ViewFeatures Features;
        public ViewAnimationProfile Animation;
        public bool KeepAliveOnDead;
        // Art axis correction, applied as a local rotation on top of the logic yaw.
        // The core yaw convention already matches Unity (0 = +Z, positive turns
        // towards +X), so +Z-forward art keeps 0 here; only art authored along another
        // axis needs a value (+X art: -90, -Z art: 180, -X art: 90).
        public float ModelYawOffsetDeg;
        // Visual-only roll about local Y for runtime bodies whose prefab has no
        // animation of its own (boomerang disc, spike ball, rolling grenade).
        public float SpinDegPerSec;
    }
}
