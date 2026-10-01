using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    // Own file on purpose. Unity only mints a MonoScript for the class whose name matches
    // its file name; a ScriptableObject class without one serialises as m_Script: 0 and
    // deserialises to null after a domain reload.
    [CreateAssetMenu(menuName = "Combat/Effects/Spawn Barrel")]
    public sealed class SpawnBarrelAsset : EffectAsset
    {
        [Tooltip("Forward offset from the caster, in metres.")]
        public float ForwardOffset = .55f;
        public float MaxHp = 5f;
        [Tooltip("Blueprint this effect spawns.")]
        public string BlueprintId = BuffArenaIds.BarrelBlueprint;
        [Tooltip("View blueprint published for the spawned barrel.")]
        public string ViewBlueprintId = "buff_barrel_view";
        protected override IEffect BakeNew() => new SpawnBuffArenaBarrelEffect
        {
            ForwardOffset = ForwardOffset,
            MaxHp = MaxHp,
            BlueprintId = BlueprintId,
            ViewBlueprintId = ViewBlueprintId
        };
    }
}
