using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>
    /// 角色碰撞半径。非法或非正半径回退到 0.25，避免调用方传 0 时导航判定把任何位置都当成可放置。
    /// </summary>
    public sealed class CharacterRadiusComp : Comp
    {
        public float Radius { get; private set; }

        public CharacterRadiusComp(float radius)
        {
            Radius = radius > 0f ? radius : .25f;
        }

        public void SetRadius(float radius) => Radius = radius > 0f ? radius : .25f;
    }

    /// <summary>
    /// 移动约束：把“能不能站”“能走到哪”“随机落点”从位移逻辑里抽出来，便于替换无导航实现。
    /// flying 选择地面/飞行两套规则；ignoreBorder 用于明确要越过地图边界的特例（如传送落点校验）。
    /// </summary>
    public interface IMovementConstraint
    {
        bool CanPlace(in SimVec3 position, float radius, bool flying, bool ignoreBorder = false);
        SimVec3 Resolve(in SimVec3 pivot, in SimVec3 target, float radius, bool flying, bool ignoreBorder, out bool obstructed);
        bool TryGetRandomPosition(IRandom random, float radius, bool flying, out SimVec3 position);
    }

    /// <summary>
    /// 无约束实现：任何位置都放行、Resolve 原样返回目标、随机点恒为 (0,0)。给没有导航网格的测试/训练场景用。
    /// </summary>
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

    /// <summary>
    /// 网格导航约束，维护地面与飞行两套可行走格（尺寸必须一致，否则构造即抛错）。
    /// BuffArenaMapVisuals 传入的 flyingWalkable 全为 true，所以飞行不受墙阻挡、只受地图边界阻挡；
    /// 反之地面单位在 ignoreBorder 为 false 时会被 Resolve 夹回边界内。CanPlace 还做半径感知的圆-格最近点相交检测，
    /// 因此“中心落在可走格上”并不等于“半径范围内都可行走”。
    /// </summary>
    public sealed class GridMovementConstraint : IMovementConstraint
    {
        readonly bool[,] _walkable;
        readonly bool[,] _flyingWalkable;
        readonly float _cellSize;
        readonly List<SimVec3> _spawnPoints = new List<SimVec3>(64);

        /// <summary>地面与飞行共用同一套可行走格。</summary>
        public GridMovementConstraint(bool[,] walkable, float cellSize = 1f)
            : this(walkable, walkable, cellSize)
        {
        }

        /// <summary>两套可行走格必须同尺寸；飞行格里墙的位置可以是 true，从而实现“飞越”。</summary>
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

        /// <summary>地面可行走性；越界一律返回 false，调用方无需先自行判界。</summary>
        public bool IsWalkable(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Height && _walkable[x, z];
        }

        /// <summary>按 flying 选择两套格子之一；同样把越界判为 false。</summary>
        public bool IsWalkable(int x, int z, bool flying)
        {
            if (x < 0 || x >= Width || z < 0 || z >= Height) return false;
            return (flying ? _flyingWalkable : _walkable)[x, z];
        }

        /// <summary>
        /// 判断以 position 为圆心、radius 为半径的身体能否放下：先做边界夹取（ignoreBorder 时跳过），
        /// 再对覆盖到的每个不可走格做圆-矩形最近点相交测试——只有真正重叠才算挡住，因此贴着墙角站是允许的。
        /// </summary>
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

        /// <summary>
        /// 把 pivot→target 的位移按 X、Z 轴分开求解后合成：先固定 Z=pivot.Z 解 X，再用解出的 X 作为固定轴解 Z。
        /// 轴分离是关键，它让角色能沿墙滑动而不是被整段位移卡死。obstructed 表示“某轴的期望位移被削减过，
        /// 或合成点仍不可放置”；后者会整体回退到 pivot（而不是停在半路），调用方可据此判定本次移动被完全挡下。
        /// </summary>
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

        /// <summary>
        /// 从预计算的地面出生点里挑一个“能放下”的点：随机选起点后按环形顺序向后找，
        /// 于是随机退化成“从随机起点找最近可用点”，保证不会因为随机点恰好被占就失败（全部不可用才返回 false）。
        /// </summary>
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

        /// <summary>
        /// 单轴推进：从 start 沿方向逐格检查，遇到不可走格（或越界且不允许越界）就把落点截到格子边缘再退一个半径，
        /// 再与 desired 取 min/max 保证不越过目标；全轴通畅时按 ignoreBorder 决定是否夹到地图边界。
        /// </summary>
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

        /// <summary>
        /// 只在构造时跑一次：收集地面可行走格中心作为出生点，避免运行时反复扫描整张地图。
        /// </summary>
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
