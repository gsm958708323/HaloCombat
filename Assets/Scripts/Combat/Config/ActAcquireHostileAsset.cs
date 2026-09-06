using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/AcquireHostile")] public sealed class ActAcquireHostileAsset : BtNodeAsset { public override BtNode Bake() => new ActAcquireHostile(); } }
