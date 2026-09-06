using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/StartReturn")] public sealed class ActStartReturnAsset : BtNodeAsset { public override BtNode Bake() => new ActStartReturn(); } }
