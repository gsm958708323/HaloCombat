using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    // Own file on purpose. Unity only mints a MonoScript for the class whose name matches
    // its file name; a ScriptableObject class without one serialises as m_Script: 0 and
    // deserialises to null after a domain reload.
    [CreateAssetMenu(menuName = "Combat/Effects/Refill Ammo")]
    public sealed class RefillAmmoAsset : EffectAsset
    {
        public int Amount = 60;
        protected override IEffect BakeNew() => new RefillAmmoEffect(Amount);
    }
}
