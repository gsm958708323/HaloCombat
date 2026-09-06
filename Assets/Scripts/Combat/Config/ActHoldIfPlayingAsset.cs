using Combat.Core;
using UnityEngine;
namespace Combat.Config { [CreateAssetMenu(menuName = "Combat/BT/Action/HoldIfPlaying")] public sealed class ActHoldIfPlayingAsset : BtNodeAsset { public override BtNode Bake() => new ActHoldIfPlaying(); } }
