using System.Collections.Generic;
using Combat.Unity.Game;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class BuffArenaMapGenerationTests
    {
        GameObject _mapObject;

        [TearDown]
        public void TearDown()
        {
            if (_mapObject != null) Object.DestroyImmediate(_mapObject);
        }

        [Test]
        public void LayoutsKeepConnectedLandAndOpenCombatRoutes()
        {
            _mapObject = new GameObject("MapTest");
            var map = _mapObject.AddComponent<BuffArenaMapVisuals>();

            for (int seed = 1; seed <= 50; seed++)
            {
                map.Seed = seed;
                map.Build();
                var visited = new bool[map.Width, map.Height];
                var queue = new Queue<Vector2Int>();
                int centerX = map.Width / 2;
                int centerZ = map.Height / 2;
                queue.Enqueue(new Vector2Int(centerX, centerZ));
                visited[centerX, centerZ] = true;
                int reachable = 0;
                int total = 0;

                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    reachable++;
                    Enqueue(map, visited, queue, cell.x - 1, cell.y);
                    Enqueue(map, visited, queue, cell.x + 1, cell.y);
                    Enqueue(map, visited, queue, cell.x, cell.y - 1);
                    Enqueue(map, visited, queue, cell.x, cell.y + 1);
                }

                for (int x = 0; x < map.Width; x++)
                    for (int z = 0; z < map.Height; z++)
                        if (map.Navigation.IsWalkable(x, z)) total++;

                Assert.AreEqual(total, reachable, "Unreachable land at seed " + seed);
                Assert.That(total / (float)(map.Width * map.Height), Is.InRange(.6f, .66f),
                    "Land density at seed " + seed);
                for (int x = centerX - 2; x <= centerX + 2; x++)
                    for (int z = centerZ - 2; z <= centerZ + 2; z++)
                        Assert.IsTrue(map.Navigation.IsWalkable(x, z), "Blocked center at seed " + seed);

                Assert.IsTrue(HasLandOnSide(map, true, 1));
                Assert.IsTrue(HasLandOnSide(map, true, map.Width - 2));
                Assert.IsTrue(HasLandOnSide(map, false, 1));
                Assert.IsTrue(HasLandOnSide(map, false, map.Height - 2));
            }
        }

        [Test]
        public void SameSeedRebuildsTheSameLayout()
        {
            _mapObject = new GameObject("MapTest");
            var map = _mapObject.AddComponent<BuffArenaMapVisuals>();
            map.Seed = 73;
            map.Build();
            int width = map.Width;
            int height = map.Height;
            var original = new bool[width, height];
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    original[x, z] = map.Navigation.IsWalkable(x, z);

            map.Build();
            Assert.AreEqual(width, map.Width);
            Assert.AreEqual(height, map.Height);
            for (int x = 0; x < width; x++)
                for (int z = 0; z < height; z++)
                    Assert.AreEqual(original[x, z], map.Navigation.IsWalkable(x, z));
        }

        static bool HasLandOnSide(BuffArenaMapVisuals map, bool vertical, int coordinate)
        {
            int length = vertical ? map.Height : map.Width;
            for (int index = 1; index < length - 1; index++)
                if (vertical ? map.Navigation.IsWalkable(coordinate, index)
                    : map.Navigation.IsWalkable(index, coordinate)) return true;
            return false;
        }

        static void Enqueue(BuffArenaMapVisuals map, bool[,] visited, Queue<Vector2Int> queue, int x, int z)
        {
            if (x < 0 || x >= map.Width || z < 0 || z >= map.Height) return;
            if (visited[x, z] || !map.Navigation.IsWalkable(x, z)) return;
            visited[x, z] = true;
            queue.Enqueue(new Vector2Int(x, z));
        }
    }
}
