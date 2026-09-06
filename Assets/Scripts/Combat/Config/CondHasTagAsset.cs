using Combat.Core;
using UnityEngine;
namespace Combat.Config
{
    [CreateAssetMenu(menuName = "Combat/BT/Condition/HasTag")]
    public sealed class CondHasTagAsset : BtNodeAsset
    {
        public int TagValue = CommonTags.Dead.Value;
        public bool Invert;
        public override BtNode Bake() => new CondHasTag(new TagId(TagValue), Invert);
    }
}
