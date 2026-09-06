using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Condition/TargetHasTag")] public sealed class CondTargetHasTagAsset : BtNodeAsset { public int TagValue = CommonTags.Airborne.Value; public bool Invert; public override BtNode Bake() => new CondTargetHasTag(new TagId(TagValue), Invert); } }
