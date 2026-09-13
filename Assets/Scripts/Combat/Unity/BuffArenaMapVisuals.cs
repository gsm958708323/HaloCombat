using Combat.Core;
using UnityEngine;

namespace Combat.Unity.Game
{
    public sealed class BuffArenaMapVisuals : MonoBehaviour
    {
        public GameObject GrassPrefab;
        public GameObject WaterPrefab;
        public Transform MapRoot;
        public int Seed = 1;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public GridMovementConstraint Navigation { get; private set; }

        public GridMovementConstraint Build()
        {
            ClearMap();
            Width = UnityEngine.Random.Range(10, 15);
            Height = UnityEngine.Random.Range(10, 15);
            var walkable = new bool[Width, Height];
            var random = new System.Random(Seed == 0 ? 1 : Seed);
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++)
                    walkable[x, z] = Mathf.PerlinNoise((float)x / Width, (float)z / Height) *
                        Mathf.Lerp(10f, 20f, (float)random.NextDouble()) > 6f;
            EnsureOpenCell(walkable);
            Transform root = MapRoot != null ? MapRoot : transform;
            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Height; z++)
                {
                    bool grass = walkable[x, z];
                    var prefab = grass ? GrassPrefab : WaterPrefab;
                    if (prefab != null)
                    {
                        var tile = Instantiate(prefab, new Vector3(x, 0f, z), Quaternion.identity, root);
                        tile.name = grass ? "Grass_" + x + "_" + z : "Water_" + x + "_" + z;
                        BuffArenaRenderUtility.Normalize(tile);
                    }
                }
            }

            Navigation = new GridMovementConstraint(walkable, 1f);
            return Navigation;
        }

        void EnsureOpenCell(bool[,] walkable)
        {
            int cx = Width / 2;
            int cz = Height / 2;
            walkable[cx, cz] = true;
        }

        void ClearMap()
        {
            Transform root = MapRoot != null ? MapRoot : transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }
    }
}
