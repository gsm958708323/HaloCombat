using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/Cues")]
    public sealed class CueLibraryAsset : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int CueId;
            public string PrefabKey;
            public string SfxKey;
            public float LifeTime;
            public GameObject Prefab;
        }

        public Entry[] Entries;

        public CueLibrary Bake()
        {
            var library = new CueLibrary();
            if (Entries == null) return library;
            for (int i = 0; i < Entries.Length; i++)
            {
                var entry = Entries[i];
                library.Register(new CueDef
                {
                    CueId = entry.CueId,
                    PrefabKey = entry.PrefabKey,
                    SfxKey = entry.SfxKey,
                    LifeTime = entry.LifeTime
                });
            }
            return library;
        }

        public GameObject FindPrefab(int cueId)
        {
            if (Entries == null) return null;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].CueId == cueId) return Entries[i].Prefab;
            return null;
        }
    }
}
