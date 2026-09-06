using UnityEngine;

namespace Combat.Unity.Presentation
{
    [CreateAssetMenu(menuName = "Combat/Cue Prefab Table")]
    public sealed class CuePrefabTable : ScriptableObject
    {
        public CuePrefabEntry[] Entries;

        public GameObject Find(string key)
        {
            if (Entries == null)
                return null;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].Key == key)
                    return Entries[i].Prefab;
            return null;
        }
    }

    [System.Serializable]
    public struct CuePrefabEntry
    {
        public string Key;
        public GameObject Prefab;
    }
}
