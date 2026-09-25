using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    /// <summary>
    /// “生成一枚弹体”效果：只把 SpecId 搬进 SpawnProjectileEffect，弹道参数全部由数据库
    /// Projectiles 目录里同 SpecId 的 ProjectileDefAsset 决定。SpecId 抄错不会在这里报错——
    /// 世界发射时 TryGet 失败就静默返回（见 RuntimeBodies），所以它必须与目录条目严格对齐。
    /// 本类与文件同名，遵守“一个 SO 类 = 一个同名 .cs 文件”的约定，原因见 EffectAsset。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Effects/SpawnProjectile")]
    public sealed class SpawnProjectileAsset : EffectAsset
    {
        public int SpecId;
        // SpecId 是唯一可变字段，实例本身无状态；不在此处查找目录，找不到时静默不发弹。
        protected override IEffect BakeNew() => new SpawnProjectileEffect(SpecId);
    }
}
