using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    /// <summary>
    /// 效果资产的公共壳：子类只声明可编辑字段，并在 BakeNew() 里把这些字段显式搬成运行时 IEffect；
    /// 壳本身不校验、不缓存、也不认识任何具体效果。
    /// 硬约定：一个 ScriptableObject 子类必须独占一个同名 .cs 文件。Unity 靠 MonoScript(m_Script)
    /// 把资产关联回类型，同文件里只有与文件同名的那个类写得进去；其余资产会落盘成 m_Script: 0，
    /// 域重载后引用变成 null，而且是静默失败——所以新增效果类型时“一个类一个同名文件”不能图省事合并。
    /// </summary>
    public abstract class EffectAsset : ScriptableObject
    {
        /// <summary>构造一个全新的运行时效果实例；子类实现，字段在这一步逐项搬运。</summary>
        protected abstract IEffect BakeNew();
        /// <summary>烘焙入口。默认不缓存、每次都给新实例；需要复用的子类才覆盖它（并配套覆盖 ClearCache）。</summary>
        public virtual IEffect Bake() => BakeNew();
        /// <summary>丢弃派生缓存；默认无缓存可丢，持有子资产缓存的子类覆盖它做级联清理。</summary>
        public virtual void ClearCache() { }
    }
}
