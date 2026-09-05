using Combat.Core;
using UnityEngine;

namespace Combat.Config
{
    // Leaf assets deliberately bake a fresh runtime node on every call. Runtime
    // nodes own execution state (for example Running/Abort), so sharing them
    // between actors or retaining them on the asset would leak state.
    [CreateAssetMenu(menuName = "Combat/BT/Condition/HasTag")]
    public sealed class CondHasTagAsset : BtNodeAsset
    {
        public int TagValue = CommonTags.Dead.Value;
        public bool Invert;
        public override BtNode Bake() => new CondHasTag(new TagId(TagValue), Invert);
    }

    [CreateAssetMenu(menuName = "Combat/BT/Condition/HasTarget")]
    public sealed class CondHasTargetAsset : BtNodeAsset
    {
        public override BtNode Bake() => new CondHasTarget();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Condition/InRange")]
    public sealed class CondInRangeAsset : BtNodeAsset
    {
        public override BtNode Bake() => new CondInRange();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Condition/BeyondLeash")]
    public sealed class CondBeyondLeashAsset : BtNodeAsset
    {
        public override BtNode Bake() => new CondBeyondLeash();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Condition/OwnerDead")]
    public sealed class CondOwnerDeadAsset : BtNodeAsset
    {
        public override BtNode Bake() => new CondOwnerDead();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/StopMove")]
    public sealed class ActStopMoveAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActStopMove();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/AcquireHostile")]
    public sealed class ActAcquireHostileAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActAcquireHostile();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/MoveToward")]
    public sealed class ActMoveTowardAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActMoveToward();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/FaceTarget")]
    public sealed class ActFaceTargetAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActFaceTarget();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/StartReturn")]
    public sealed class ActStartReturnAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActStartReturn();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/HoldIfPlaying")]
    public sealed class ActHoldIfPlayingAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActHoldIfPlaying();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/MoveTowardHome")]
    public sealed class ActMoveTowardHomeAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActMoveTowardHome();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/Patrol")]
    public sealed class ActPatrolAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActPatrol();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/RequestDespawn")]
    public sealed class ActRequestDespawnAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActRequestDespawn();
    }

    [CreateAssetMenu(menuName = "Combat/BT/Action/FollowOwner")]
    public sealed class ActFollowOwnerAsset : BtNodeAsset
    {
        public override BtNode Bake() => new ActFollowOwner();
    }
}
