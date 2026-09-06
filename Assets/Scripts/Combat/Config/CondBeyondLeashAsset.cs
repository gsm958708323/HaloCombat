using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Condition/BeyondLeash")] public sealed class CondBeyondLeashAsset : BtNodeAsset { public override BtNode Bake() => new CondBeyondLeash(); } }
