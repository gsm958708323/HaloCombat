using UnityEngine;

namespace Combat.Unity.Presentation
{
    [CreateAssetMenu(menuName = "Combat/View Prefab Table")]
    public sealed class ViewPrefabTable : ScriptableObject
    {
        public ViewPrefabEntry[] Entries;

        public GameObject Find(string id)
        {
            if (Entries == null)
                return null;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].BlueprintId == id)
                    return Entries[i].Prefab;
            return null;
        }
    }

    [System.Serializable]
    public struct ViewPrefabEntry
    {
        public string BlueprintId;
        public GameObject Prefab;
    }
}
