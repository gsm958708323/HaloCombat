using System;
using System.Collections.Generic;
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
        [Range(.5f, .7f)] public float WalkableRatio = .62f;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public GridMovementConstraint Navigation { get; private set; }

        public GridMovementConstraint Build()
        {
            ClearMap();
            var random = new System.Random(Seed == 0 ? 1 : Seed);
            Width = random.Next(13, 17);
            Height = random.Next(13, 17);
            bool[,] walkable = BuildLayout(random);
            var flyingWalkable = new bool[Width, Height];
            Transform root = MapRoot != null ? MapRoot : transform;
            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Height; z++)
                {
                    flyingWalkable[x, z] = true;
                    bool grass = walkable[x, z];
                    var prefab = grass ? GrassPrefab : WaterPrefab;
                    if (prefab != null)
                    {
                        var tile = Instantiate(prefab, new Vector3(x, grass ? 0f : -.04f, z),
                            Quaternion.Euler(0f, random.Next(4) * 90f, 0f), root);
                        tile.name = grass ? "Grass_" + x + "_" + z : "Water_" + x + "_" + z;
                        BuffArenaRenderUtility.Normalize(tile);
                    }
                }
            }

            Navigation = new GridMovementConstraint(walkable, flyingWalkable, 1f);
            return Navigation;
        }

        bool[,] BuildLayout(System.Random random)
        {
            var land = new bool[Width, Height];
            int openCount = 0;
            int centerX = Width / 2;
            int centerZ = Height / 2;

            for (int x = centerX - 2; x <= centerX + 2; x++)
                for (int z = centerZ - 2; z <= centerZ + 2; z++)
                    Open(land, x, z, ref openCount);

            CarveRoute(land, centerX, centerZ, 1, random.Next(2, Height - 2), random, ref openCount);
            CarveRoute(land, centerX, centerZ, Width - 2, random.Next(2, Height - 2), random, ref openCount);
            CarveRoute(land, centerX, centerZ, random.Next(2, Width - 2), 1, random, ref openCount);
            CarveRoute(land, centerX, centerZ, random.Next(2, Width - 2), Height - 2, random, ref openCount);

            var ponds = new bool[Width, Height];
            for (int pond = 0; pond < 2; pond++)
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    int x = random.Next(2, Width - 3);
                    int z = random.Next(2, Height - 3);
                    if (x <= centerX + 2 && x + 1 >= centerX - 2 &&
                        z <= centerZ + 2 && z + 1 >= centerZ - 2) continue;
                    if (land[x, z] || land[x + 1, z] || land[x, z + 1] || land[x + 1, z + 1] ||
                        ponds[x, z] || ponds[x + 1, z] || ponds[x, z + 1] || ponds[x + 1, z + 1]) continue;
                    ponds[x, z] = ponds[x + 1, z] = ponds[x, z + 1] = ponds[x + 1, z + 1] = true;
                    break;
                }

            var frontier = new List<Vector2Int>(Width * Height);
            var queued = new bool[Width, Height];
            for (int x = 1; x < Width - 1; x++)
                for (int z = 1; z < Height - 1; z++)
                    if (land[x, z]) AddNeighbors(land, ponds, queued, frontier, x, z);

            int target = Mathf.RoundToInt(Width * Height * Mathf.Clamp(WalkableRatio, .5f, .7f));
            while (openCount < target && frontier.Count > 0)
            {
                int index = random.Next(frontier.Count);
                Vector2Int cell = frontier[index];
                frontier[index] = frontier[frontier.Count - 1];
                frontier.RemoveAt(frontier.Count - 1);
                if (land[cell.x, cell.y] || ponds[cell.x, cell.y]) continue;
                Open(land, cell.x, cell.y, ref openCount);
                AddNeighbors(land, ponds, queued, frontier, cell.x, cell.y);
            }

            return land;
        }

        void CarveRoute(bool[,] land, int x, int z, int targetX, int targetZ,
            System.Random random, ref int openCount)
        {
            while (x != targetX || z != targetZ)
            {
                bool stepX = x != targetX && (z == targetZ || random.Next(2) == 0);
                if (stepX) x += Math.Sign(targetX - x);
                else z += Math.Sign(targetZ - z);
                Open(land, x, z, ref openCount);
                if (random.Next(2) == 0)
                {
                    if (stepX) Open(land, x, z + (random.Next(2) == 0 ? -1 : 1), ref openCount);
                    else Open(land, x + (random.Next(2) == 0 ? -1 : 1), z, ref openCount);
                }
            }
        }

        void Open(bool[,] land, int x, int z, ref int openCount)
        {
            if (x <= 0 || x >= Width - 1 || z <= 0 || z >= Height - 1 || land[x, z]) return;
            land[x, z] = true;
            openCount++;
        }

        void AddNeighbors(bool[,] land, bool[,] ponds, bool[,] queued, List<Vector2Int> frontier, int x, int z)
        {
            AddNeighbor(land, ponds, queued, frontier, x - 1, z);
            AddNeighbor(land, ponds, queued, frontier, x + 1, z);
            AddNeighbor(land, ponds, queued, frontier, x, z - 1);
            AddNeighbor(land, ponds, queued, frontier, x, z + 1);
        }

        void AddNeighbor(bool[,] land, bool[,] ponds, bool[,] queued, List<Vector2Int> frontier, int x, int z)
        {
            if (x <= 0 || x >= Width - 1 || z <= 0 || z >= Height - 1 ||
                land[x, z] || ponds[x, z] || queued[x, z]) return;
            queued[x, z] = true;
            frontier.Add(new Vector2Int(x, z));
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
