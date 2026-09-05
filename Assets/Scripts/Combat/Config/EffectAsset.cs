using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    public abstract class EffectAsset : ScriptableObject
    {
        protected abstract IEffect BakeNew();
        public virtual IEffect Bake() => BakeNew();
        public virtual void ClearCache() { }
    }
}
