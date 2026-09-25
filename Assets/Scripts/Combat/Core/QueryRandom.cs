using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>随机源抽象。战斗内随机一律走这里，回放/测试时才能替换成确定的序列。</summary>
    public interface IRandom
    {
        float Next01();
    }

    /// <summary>固定种子伪随机：同一种子、同一调用顺序得到同一序列，用于复现与回放。</summary>
    public sealed class SeededRandom : IRandom
    {
        readonly Random _rng;
        public SeededRandom(int seed = 1) => _rng = new Random(seed);
        public float Next01() => (float)_rng.NextDouble();
    }

    /// <summary>恒定返回同一个 [0,1) 值：用于测试里强制命中或不命中某个概率分支。</summary>
    public sealed class FixedRandom : IRandom
    {
        readonly float _value;
        public FixedRandom(float value01) => _value = value01;
        public float Next01() => _value;
    }

    /// <summary>目标查询接口。实现必须只读世界，不得在查询过程中改实体状态——查询会在伤害结算等可能重入的路径里被调用。</summary>
    public interface ITargetQuery
    {
        int OverlapCircle(SimVec3 center, float radius, Actor source, int hostileMask, List<Actor> results);
        int OverlapFan(SimVec3 origin, float yawDegrees, float radius, float halfAngleDegrees, Actor source, int hostileMask, List<Actor> results);
    }

    /// <summary>线性遍历活动实体的查询实现；内部 _actors 只作自身暂存，results 由调用方提供并会被先 Clear。</summary>
    public sealed class SimpleTargetQuery : ITargetQuery
    {
        CombatWorld _world;
        readonly List<Actor> _actors = new List<Actor>(64);
        public void Bind(CombatWorld world) => _world = world;

        /// <summary>
        /// 圆形范围查询：先清空 results 再写入命中者，返回命中数量。
        /// 有效半径 = 查询半径 + 目标自身半径（CharacterRadiusComp，缺省 0），即按中心到中心的距离判定，所以贴边接触也算命中。
        /// results 属于调用方：本方法只负责清空并填充，绝不缓存或跨调用共享它——同一帧内木桶连锁爆炸会重入本方法，
        /// 共用一个 List 会让内层结果覆盖掉外层正在遍历的数据。
        /// </summary>
        public int OverlapCircle(SimVec3 center, float radius, Actor source, int hostileMask, List<Actor> results)
        {
            if (_world == null || results == null) return 0;
            results.Clear();
            _world.RegistryActive(_actors);
            var actors = _actors;
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (a == null || !a.IsActive || a == source) continue;
                if (!PassesFilter(source, a, hostileMask)) continue;
                if (!a.TryGetComp<TransformComp>(out var tf)) continue;
                float dx = tf.Position.X - center.X;
                float dz = tf.Position.Z - center.Z;
                float targetRadius = a.TryGetComp<CharacterRadiusComp>(out var body) ? body.Radius : 0f;
                float reach = radius + targetRadius;
                if (dx * dx + dz * dz > reach * reach) continue;
                results.Add(a);
            }

            return results.Count;
        }

        /// <summary>扇形查询：先做圆查询，再按与 yawDegrees 的夹角不超过 halfAngleDegrees 过滤，原地压缩写回 results，不额外分配。</summary>
        public int OverlapFan(SimVec3 origin, float yawDegrees, float radius, float halfAngleDegrees, Actor source, int hostileMask, List<Actor> results)
        {
            int n = OverlapCircle(origin, radius, source, hostileMask, results);
            if (n <= 0) return 0;
            float half = halfAngleDegrees < 0f ? 0f : halfAngleDegrees;
            int w = 0;
            for (int i = 0; i < n; i++)
            {
                var tf = results[i].GetComp<TransformComp>();
                float dx = tf.Position.X - origin.X;
                float dz = tf.Position.Z - origin.Z;
                float yaw = LocomotionComp.YawFromStick(new SimVec3(dx, 0f, dz));
                float delta = NormalizeAngle(yaw - yawDegrees);
                if (Math.Abs(delta) <= half)
                    results[w++] = results[i];
            }

            if (w < results.Count)
                results.RemoveRange(w, results.Count - w);
            return w;
        }

        /// <summary>
        /// 目标过滤，三条规则：
        /// 1) 运行时体（带 ProjectileComp 或 AoeComp）永不参战——它们带 TeamComp 只为继承归属；
        /// 2) 挂 Dead 标签的排除；
        /// 3) 阵营判定：hostileMask 非 0 时按位掩码（1 左移 TeamId），为 0 时回落到 source 的 TeamComp.IsHostileTo。
        /// 另外没有 TeamComp 的目标直接出局：中立物件不该被敌对查询选中。
        /// </summary>
        static bool PassesFilter(Actor source, Actor target, int hostileMask)
        {
            // Runtime bodies are not combatants. They carry a TeamComp only so
            // spawned projectiles/AoEs can inherit ownership, but must never become
            // AI or hit-scan targets themselves.
            if (target.TryGetComp<ProjectileComp>(out _) || target.TryGetComp<AoeComp>(out _))
                return false;
            if (target.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead))
                return false;
            if (!target.TryGetComp<TeamComp>(out var tt))
                return false;
            if (hostileMask != 0)
                return (hostileMask & (1 << tt.TeamId)) != 0;
            if (source == null || !source.TryGetComp<TeamComp>(out var st))
                return false;
            return st.IsHostileTo(tt);
        }

        /// <summary>把角度归一到 ±180 以内，避免绕圈导致与 halfAngle 的比较大范围误判。</summary>
        static float NormalizeAngle(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
        }
    }

    /// <summary>判定盒命中服务：每帧扫描所有打开的 HitboxComp 做一次圆查询，同一攻击者对同一目标只结算一次（TryRecord 去重）。</summary>
    public sealed class HitDetectService
    {
        readonly CombatWorld _world;
        readonly List<Actor> _actors = new List<Actor>(64);
        readonly List<Actor> _buffer = new List<Actor>(64);

        public HitDetectService(CombatWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>每帧扫描打开的判定盒：快照 Atk 取攻击者当帧最终 Atk，命中后投递 ApplyEffectsIntent，具体伤害由效果管线异步结算。</summary>
        public void Tick()
        {
            _world.RegistryActive(_actors);
            var actors = _actors;
            for (int i = 0; i < actors.Count; i++)
            {
                var attacker = actors[i];
                if (!attacker.TryGetComp<HitboxComp>(out var box) || !box.IsOpen) continue;
                if (box.BakedOnHit == null || box.BakedOnHit.Length == 0) continue;
                if (!attacker.TryGetComp<TransformComp>(out var tf)) continue;

                var center = CombatGeom.HitboxCenter(tf, box);
                int n = _world.Query.OverlapCircle(center, box.Radius, attacker, 0, _buffer);
                float snapshotAtk = 0f;
                if (attacker.TryGetComp<AttributeSet>(out var attr))
                    snapshotAtk = attr.GetFinal(AttrId.Atk);

                for (int k = 0; k < n; k++)
                {
                    var victim = _buffer[k];
                    if (!box.TryRecord(victim.Id)) continue;
                    var point = victim.TryGetComp<TransformComp>(out var vtf) ? vtf.Position : center;
                    _world.Intents.Post(new ApplyEffectsIntent(
                        box.BakedOnHit, attacker.Id, victim.Id, snapshotAtk, 0, point, true));
                }
            }
        }

    }
}
