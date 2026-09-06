using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/FaceTarget")] public sealed class ActFaceTargetAsset : BtNodeAsset { public override BtNode Bake() => new ActFaceTarget(); } }
