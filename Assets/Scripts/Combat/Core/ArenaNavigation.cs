using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class CharacterRadiusComp : Comp
    {
        public float Radius { get; private set; }

        public CharacterRadiusComp(float radius)
        {
            Radius = radius > 0f ? radius : .25f;
        }

        public void SetRadius(float radius) => Radius = radius > 0f ? radius : .25f;
    }

    public interface IMovementConstraint
    {
        bool CanPlace(in SimVec3 position, float radius, bool flying, bool ignoreBorder = false);
        SimVec3 Resolve(in SimVec3 pivot, in SimVec3 target, float radius, bool flying, bool ignoreBorder, out bool obstructed);
        bool TryGetRandomPosition(IRandom random, float radius, bool flying, out SimVec3 position);
    }

    public sealed class FreeMovementConstraint : IMovementConstraint
    {
        public bool CanPlace(in SimVec3 position, float radius, bool flying, bool ignoreBorder = false)
        {
            _ = position;
            _ = radius;
            _ = flying;
            _ = ignoreBorder;
            return true;
        }

        public SimVec3 Resolve(in SimVec3 pivot, in SimVec3 target, float radius, bool flying, bool ignoreBorder, out bool obstructed)
        {
            _ = pivot;
            _ = radius;
            _ = flying;
            _ = ignoreBorder;
            obstructed = false;
            return target;
        }

        public bool TryGetRandomPosition(IRandom random, float radius, bool flying, out SimVec3 position)
        {
            _ = random;
            _ = radius;
            _ = flying;
            position = SimVec3.Zero;
            return true;
        }
    }

    public sealed class GridMovementConstraint : IMovementConstraint
    {
        readonly bool[,] _walkable;
        readonly bool[,] _flyingWalkable;
        readonly float _cellSize;
        readonly List<SimVec3> _spawnPoints = new List<SimVec3>(64);

        public GridMovementConstraint(bool[,] walkable, float cellSize = 1f)
            : this(walkable, walkable, cellSize)
        {
        }

        public GridMovementConstraint(bool[,] walkable, bool[,] flyingWalkable, float cellSize = 1f)
        {
            _walkable = walkable ?? throw new ArgumentNullException(nameof(walkable));
            _flyingWalkable = flyingWalkable ?? throw new ArgumentNullException(nameof(flyingWalkable));
            if (_flyingWalkable.GetLength(0) != _walkable.GetLength(0) ||
                _flyingWalkable.GetLength(1) != _walkable.GetLength(1))
                throw new ArgumentException("Flying navigation dimensions must match ground navigation.", nameof(flyingWalkable));
            _cellSize = cellSize > 0f ? cellSize : 1f;
            BuildSpawnPoints();
        }

        public int Width => _walkable.GetLength(0);
        public int Height => _walkable.GetLength(1);

        public bool IsWalkable(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Height && _walkable[x, z];
        }

        public bool IsWalkable(int x, int z, bool flying)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Height) return false;
            return (flying ? _flyingWalkable : _walkable)[x, z];
        }

        public bool CanPlace(in SimVec3 position, float radius, bool flying, bool ignoreBorder = false)
        {
            float r = radius < 0f ? 0f : radius;
            if (!ignoreBorder &&
                (position.X - r < -_cellSize * .5f ||
                 position.Z - r < -_cellSize * .5f ||
                 position.X + r > (Width - .5f) * _cellSize ||
                 position.Z + r > (Height - .5f) * _cellSize))
                return false;

            int minX = Cell(position.X - r);
            int maxX = Cell(position.X + r);
            int minZ = Cell(position.Z - r);
            int maxZ = Cell(position.Z + r);
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    if (IsWalkable(x, z, flying)) continue;
                    float left = (x - .5f) * _cellSize;
                    float right = (x + .5f) * _cellSize;
                    float bottom = (z - .5f) * _cellSize;
                    float top = (z + .5f) * _cellSize;
                    float closestX = Clamp(position.X, left, right);
                    float closestZ = Clamp(position.Z, bottom, top);
                    float dx = position.X - closestX;
                    float dz = position.Z - closestZ;
                    if (dx * dx + dz * dz <= r * r)
                        return false;
                }
            }

            return true;
        }

        public SimVec3 Resolve(in SimVec3 pivot, in SimVec3 target, float radius, bool flying, bool ignoreBorder, out bool obstructed)
        {
            SimVec3 result = target;
            obstructed = false;

            float x = ResolveAxis(pivot.X, pivot.Z, target.X - pivot.X, radius, true, flying, ignoreBorder);
            if (Math.Abs(x - target.X) > .0001f) obstructed = true;
            result.X = x;

            float z = ResolveAxis(pivot.Z, result.X, target.Z - pivot.Z, radius, false, flying, ignoreBorder);
            if (Math.Abs(z - target.Z) > .0001f) obstructed = true;
            result.Z = z;

            if (!CanPlace(result, radius, flying, ignoreBorder))
            {
                result.X = pivot.X;
                result.Z = pivot.Z;
                obstructed = true;
            }

            return result;
        }

        public bool TryGetRandomPosition(IRandom random, float radius, bool flying, out SimVec3 position)
        {
            position = SimVec3.Zero;
            if (_spawnPoints.Count == 0) return false;
            int index = random == null ? 0 : (int)(random.Next01() * _spawnPoints.Count);
            if (index < 0) index = 0;
            if (index >= _spawnPoints.Count) index = _spawnPoints.Count - 1;

            // A radius-aware search keeps the source game's grid spawn points
            // valid for both characters and runtime bodies.
            for (int i = 0; i < _spawnPoints.Count; i++)
            {
                var candidate = _spawnPoints[(index + i) % _spawnPoints.Count];
                if (CanPlace(candidate, radius, flying))
                {
                    position = candidate;
                    return true;
                }
            }

            return false;
        }

        float ResolveAxis(float start, float fixedAxis, float delta, float radius, bool xAxis, bool flying, bool ignoreBorder)
        {
            if (Math.Abs(delta) < .000001f) return start;
            int direction = delta > 0f ? 1 : -1;
            float desired = start + delta;
            int first = Cell(start);
            int last = Cell(desired);
            int stepCount = Math.Abs(last - first) + 2;
            for (int i = 0; i <= stepCount; i++)
            {
                int cell = first + direction * i;
                int x = xAxis ? cell : Cell(fixedAxis);
                int z = xAxis ? Cell(fixedAxis) : cell;
                if (IsWalkable(x, z, flying) && (ignoreBorder || InBounds(x, z))) continue;

                float wall = (cell - direction * .5f) * _cellSize - direction * Math.Max(0f, radius);
                return direction > 0 ? Math.Min(wall, desired) : Math.Max(wall, desired);
            }

            if (!ignoreBorder)
            {
                float min = -_cellSize * .5f + Math.Max(0f, radius);
                float max = (xAxis ? Width : Height) * _cellSize - _cellSize * .5f - Math.Max(0f, radius);
                return Clamp(desired, min, max);
            }

            return desired;
        }

        int Cell(float value) => (int)Math.Floor(value / _cellSize + .5f);

        bool InBounds(int x, int z) => x >= 0 && x < Width && z >= 0 && z < Height;

        void BuildSpawnPoints()
        {
            for (int x = 0; x < Width; x++)
                for (int z = 0; z < Height; z++)
                    if (_walkable[x, z])
                        _spawnPoints.Add(new SimVec3(x * _cellSize, 0f, z * _cellSize));
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
