using System;

namespace Combat.Core
{
    public enum BtStatus : byte { Failure = 0, Success = 1, Running = 2 }

    public readonly struct BtTick
    {
        public readonly Actor Self;
        public readonly CombatWorld World;
        public readonly BtBlackboard Board;
        public readonly float Dt;
        public BtTick(Actor self, CombatWorld world, BtBlackboard board, float dt)
        {
            Self = self; World = world; Board = board; Dt = dt;
        }
    }

    public sealed class BtBlackboard
    {
        public EntityId Target;
        public EntityId Owner;
        public SimVec3 Home;
        public float AttackRange = 1.15f;
        public float AcquireRadius = 8f;
        public float FollowRange = 2f;
        public float LeashRange = 6f;
        public float PatrolRadius = 1.5f;
        public float ArriveRange = 0.2f;
        public bool Returning;
        public void ClearTarget() => Target = EntityId.Invalid;
    }

    public abstract class BtNode
    {
        public abstract BtStatus Tick(in BtTick ctx);
        public virtual void Abort(in BtTick ctx) { }
        public abstract BtNode Clone();
    }

    public sealed class BtSelector : BtNode
    {
        readonly BtNode[] _children;
        int _running = -1;
        public BtSelector(params BtNode[] children) => _children = children ?? Array.Empty<BtNode>();
        public override BtStatus Tick(in BtTick ctx)
        {
            for (int i = 0; i < _children.Length; i++)
            {
                var status = _children[i].Tick(ctx);
                if (status == BtStatus.Running)
                {
                    if (_running >= 0 && _running != i) _children[_running].Abort(ctx);
                    _running = i;
                    return status;
                }
                if (status == BtStatus.Success)
                {
                    if (_running >= 0 && _running != i) _children[_running].Abort(ctx);
                    _running = -1;
                    return status;
                }
            }
            if (_running >= 0) { _children[_running].Abort(ctx); _running = -1; }
            return BtStatus.Failure;
        }
        public override void Abort(in BtTick ctx)
        {
            if (_running >= 0) _children[_running].Abort(ctx);
            _running = -1;
        }
        public override BtNode Clone()
        {
            var children = new BtNode[_children.Length];
            for (int i = 0; i < children.Length; i++) children[i] = _children[i].Clone();
            return new BtSelector(children);
        }
    }

    public sealed class BtSequence : BtNode
    {
        readonly BtNode[] _children;
        int _index;
        public BtSequence(params BtNode[] children) => _children = children ?? Array.Empty<BtNode>();
        public override BtStatus Tick(in BtTick ctx)
        {
            if (_children.Length == 0) return BtStatus.Success;
            while (_index < _children.Length)
            {
                var status = _children[_index].Tick(ctx);
                if (status == BtStatus.Running) return status;
                if (status == BtStatus.Failure)
                {
                    _children[_index].Abort(ctx);
                    _index = 0;
                    return status;
                }
                _index++;
            }
            _index = 0;
            return BtStatus.Success;
        }
        public override void Abort(in BtTick ctx)
        {
            if (_index < _children.Length) _children[_index].Abort(ctx);
            _index = 0;
        }
        public override BtNode Clone()
        {
            var children = new BtNode[_children.Length];
            for (int i = 0; i < children.Length; i++) children[i] = _children[i].Clone();
            return new BtSequence(children);
        }
    }

    public sealed class CondHasTag : BtNode
    {
        readonly TagId _tag; readonly bool _invert;
        public CondHasTag(TagId tag, bool invert = false) { _tag = tag; _invert = invert; }
        public override BtStatus Tick(in BtTick ctx)
        {
            bool has = ctx.Self.TryGetComp<TagComp>(out var tags) && tags.Has(_tag);
            return (_invert ? !has : has) ? BtStatus.Success : BtStatus.Failure;
        }
        public override BtNode Clone() => new CondHasTag(_tag, _invert);
    }

    public sealed class CondHasTarget : BtNode
    {
        public override BtStatus Tick(in BtTick ctx) => IsTargetValid(ctx) ? BtStatus.Success : BtStatus.Failure;
        public static bool IsTargetValid(in BtTick ctx)
        {
            if (ctx.World == null || !ctx.Board.Target.IsValid ||
                !ctx.World.TryGetActor(ctx.Board.Target, out var t) || t == null || !t.IsActive)
                return false;
            if (ctx.Self.TryGetComp<TeamComp>(out var selfTeam) && t.TryGetComp<TeamComp>(out var targetTeam) &&
                !selfTeam.IsHostileTo(targetTeam))
                return false;
            return !t.TryGetComp<TagComp>(out var tags) || !tags.Has(CommonTags.Dead);
        }
        public override BtNode Clone() => new CondHasTarget();
    }

    public sealed class CondInRange : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (!TryDist(ctx, out var d2)) return BtStatus.Failure;
            return d2 <= ctx.Board.AttackRange * ctx.Board.AttackRange ? BtStatus.Success : BtStatus.Failure;
        }
        public static bool TryDist(in BtTick ctx, out float distSq)
        {
            distSq = float.MaxValue;
            if (!CondHasTarget.IsTargetValid(ctx) || !ctx.Self.TryGetComp<TransformComp>(out var a) ||
                !ctx.World.TryGetActor(ctx.Board.Target, out var t) || !t.TryGetComp<TransformComp>(out var b)) return false;
            float dx = b.Position.X - a.Position.X, dz = b.Position.Z - a.Position.Z;
            distSq = dx * dx + dz * dz;
            return true;
        }
        public override BtNode Clone() => new CondInRange();
    }

    public sealed class ActStopMove : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (ctx.Self.TryGetComp<LocomotionComp>(out var loco)) loco.RequestMoveIntent(0f, 0f);
            return BtStatus.Success;
        }
        public override BtNode Clone() => new ActStopMove();
    }

    public sealed class ActAcquireHostile : BtNode
    {
        readonly Actor[] _buffer = new Actor[32];
        public override BtStatus Tick(in BtTick ctx)
        {
            if (CondHasTarget.IsTargetValid(ctx)) return BtStatus.Success;
            if (ctx.Self.TryGetComp<PerceptionComp>(out var perception) && perception.TryScan(ctx.Board.AcquireRadius))
                return CondHasTarget.IsTargetValid(ctx) ? BtStatus.Success : BtStatus.Failure;
            if (ctx.World == null || !ctx.Self.TryGetComp<TransformComp>(out var tf)) return BtStatus.Failure;
            int count = ctx.World.Query.OverlapCircle(tf.Position, ctx.Board.AcquireRadius, ctx.Self, 0, _buffer);
            float best = float.MaxValue; EntityId picked = EntityId.Invalid;
            for (int i = 0; i < count; i++)
            {
                var v = _buffer[i];
                if (v == null || !v.TryGetComp<TransformComp>(out var vt)) continue;
                float dx = vt.Position.X - tf.Position.X, dz = vt.Position.Z - tf.Position.Z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; picked = v.Id; }
            }
            ctx.Board.Target = picked;
            return picked.IsValid ? BtStatus.Success : BtStatus.Failure;
        }
        public override BtNode Clone() => new ActAcquireHostile();
    }

    public sealed class ActMoveToward : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (!CondHasTarget.IsTargetValid(ctx) || !ctx.Self.TryGetComp<LocomotionComp>(out var loco) ||
                !ctx.Self.TryGetComp<TransformComp>(out var tf) || !ctx.World.TryGetActor(ctx.Board.Target, out var t) ||
                !t.TryGetComp<TransformComp>(out var targetTf)) return BtStatus.Failure;
            float dx = targetTf.Position.X - tf.Position.X, dz = targetTf.Position.Z - tf.Position.Z;
            if (dx * dx + dz * dz <= ctx.Board.AttackRange * ctx.Board.AttackRange)
            {
                loco.RequestMoveIntent(0f, 0f); return BtStatus.Success;
            }
            loco.RequestMoveIntent(dx, dz); return BtStatus.Running;
        }
        public override void Abort(in BtTick ctx)
        {
            if (ctx.Self != null && ctx.Self.TryGetComp<LocomotionComp>(out var loco)) loco.RequestMoveIntent(0f, 0f);
        }
        public override BtNode Clone() => new ActMoveToward();
    }

    public sealed class ActFaceTarget : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (!CondHasTarget.IsTargetValid(ctx) || !ctx.Self.TryGetComp<LocomotionComp>(out var loco) ||
                !ctx.Self.TryGetComp<TransformComp>(out var tf) || !ctx.World.TryGetActor(ctx.Board.Target, out var t) ||
                !t.TryGetComp<TransformComp>(out var targetTf)) return BtStatus.Failure;
            loco.RequestSnapYawDegrees(LocomotionComp.YawFromStick(new SimVec3(
                targetTf.Position.X - tf.Position.X, 0f, targetTf.Position.Z - tf.Position.Z)));
            return BtStatus.Success;
        }
        public override BtNode Clone() => new ActFaceTarget();
    }

    public sealed class ActPlaySkill : BtNode
    {
        readonly SkillNodeId _skill; readonly TimelineId _timeline; bool _playing;
        public ActPlaySkill(SkillNodeId skill, TimelineId timeline) { _skill = skill; _timeline = timeline; }
        public override BtStatus Tick(in BtTick ctx)
        {
            if (!ctx.Self.TryGetComp<SkillDirectorComp>(out var dir)) return BtStatus.Failure;
            Season2Contracts.EnsureAiMustNotStopDirector();
            if (_playing)
            {
                if (dir.IsPlaying) return BtStatus.Running;
                _playing = false; return BtStatus.Success;
            }
            if (dir.IsPlaying) return BtStatus.Running;
            if (ctx.Self.TryGetComp<TagComp>(out var tags) &&
                (tags.Has(CommonTags.Dead) || tags.Has(CommonTags.Stunned) || tags.Has(CommonTags.Downed) || tags.Has(CommonTags.Silence)))
                return BtStatus.Failure;
            if (!dir.Play(_skill, _timeline)) return BtStatus.Failure;
            _playing = true; return BtStatus.Running;
        }
        public override void Abort(in BtTick ctx) { _playing = false; }
        public override BtNode Clone() => new ActPlaySkill(_skill, _timeline);
    }

    public sealed class CondBeyondLeash : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (!ctx.Self.TryGetComp<TransformComp>(out var tf)) return BtStatus.Failure;
            float dx = tf.Position.X - ctx.Board.Home.X, dz = tf.Position.Z - ctx.Board.Home.Z;
            return dx * dx + dz * dz > ctx.Board.LeashRange * ctx.Board.LeashRange ? BtStatus.Success : BtStatus.Failure;
        }
        public override BtNode Clone() => new CondBeyondLeash();
    }

    public sealed class ActStartReturn : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            ctx.Board.Returning = true; ctx.Board.ClearTarget();
            if (ctx.Self.TryGetComp<PerceptionComp>(out var perception)) perception.ClearAlert();
            return BtStatus.Success;
        }
        public override BtNode Clone() => new ActStartReturn();
    }

    public sealed class ActHoldIfPlaying : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
            => ctx.Self.TryGetComp<SkillDirectorComp>(out var dir) && dir.IsPlaying ? BtStatus.Running : BtStatus.Failure;
        public override BtNode Clone() => new ActHoldIfPlaying();
    }

    public sealed class ActMoveTowardHome : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (!ctx.Self.TryGetComp<LocomotionComp>(out var loco) || !ctx.Self.TryGetComp<TransformComp>(out var tf)) return BtStatus.Failure;
            float dx = ctx.Board.Home.X - tf.Position.X, dz = ctx.Board.Home.Z - tf.Position.Z;
            if (dx * dx + dz * dz <= ctx.Board.ArriveRange * ctx.Board.ArriveRange)
            {
                loco.RequestMoveIntent(0f, 0f); ctx.Board.Returning = false; return BtStatus.Success;
            }
            loco.RequestMoveIntent(dx, dz); return BtStatus.Running;
        }
        public override void Abort(in BtTick ctx)
        {
            if (ctx.Self != null && ctx.Self.TryGetComp<LocomotionComp>(out var loco)) loco.RequestMoveIntent(0f, 0f);
        }
        public override BtNode Clone() => new ActMoveTowardHome();
    }

    public sealed class ActPatrol : BtNode
    {
        SimVec3 _dest; bool _init; int _sign = 1;
        public override BtStatus Tick(in BtTick ctx)
        {
            if (!ctx.Self.TryGetComp<LocomotionComp>(out var loco) || !ctx.Self.TryGetComp<TransformComp>(out var tf)) return BtStatus.Failure;
            if (!_init) { _dest = new SimVec3(ctx.Board.Home.X + ctx.Board.PatrolRadius, ctx.Board.Home.Y, ctx.Board.Home.Z); _init = true; }
            float dx = _dest.X - tf.Position.X, dz = _dest.Z - tf.Position.Z;
            if (dx * dx + dz * dz <= ctx.Board.ArriveRange * ctx.Board.ArriveRange)
            {
                _sign = -_sign; _dest = new SimVec3(ctx.Board.Home.X + _sign * ctx.Board.PatrolRadius, ctx.Board.Home.Y, ctx.Board.Home.Z);
                dx = _dest.X - tf.Position.X; dz = _dest.Z - tf.Position.Z;
            }
            loco.RequestMoveIntent(dx, dz); return BtStatus.Running;
        }
        public override void Abort(in BtTick ctx)
        {
            if (ctx.Self != null && ctx.Self.TryGetComp<LocomotionComp>(out var loco)) loco.RequestMoveIntent(0f, 0f);
        }
        public override BtNode Clone() => new ActPatrol();
    }

    public sealed class CondOwnerDead : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (!ctx.Board.Owner.IsValid || ctx.World == null || !ctx.World.TryGetActor(ctx.Board.Owner, out var owner) || owner == null || !owner.IsActive)
                return BtStatus.Success;
            return owner.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead) ? BtStatus.Success : BtStatus.Failure;
        }
        public override BtNode Clone() => new CondOwnerDead();
    }

    public sealed class ActRequestDespawn : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (ctx.World != null && ctx.Self != null) { ctx.World.RequestDespawn(ctx.Self.Id); ctx.Self.SetActive(false); }
            return BtStatus.Success;
        }
        public override BtNode Clone() => new ActRequestDespawn();
    }

    public sealed class ActFollowOwner : BtNode
    {
        public override BtStatus Tick(in BtTick ctx)
        {
            if (ctx.World == null || !ctx.Self.TryGetComp<LocomotionComp>(out var loco) || !ctx.Self.TryGetComp<TransformComp>(out var tf) ||
                !ctx.World.TryGetActor(ctx.Board.Owner, out var owner) || owner == null || !owner.TryGetComp<TransformComp>(out var ot)) return BtStatus.Failure;
            float dx = ot.Position.X - tf.Position.X, dz = ot.Position.Z - tf.Position.Z;
            if (dx * dx + dz * dz <= ctx.Board.FollowRange * ctx.Board.FollowRange) loco.RequestMoveIntent(0f, 0f);
            else loco.RequestMoveIntent(dx, dz);
            return BtStatus.Running;
        }
        public override void Abort(in BtTick ctx)
        {
            if (ctx.Self != null && ctx.Self.TryGetComp<LocomotionComp>(out var loco)) loco.RequestMoveIntent(0f, 0f);
        }
        public override BtNode Clone() => new ActFollowOwner();
    }

    public static class BtFactory
    {
        public static BtNode CombatMelee(SkillNodeId skill, TimelineId timeline)
            => new BtSelector(new BtSequence(new CondInRange(), new ActStopMove(), new ActFaceTarget(), new ActPlaySkill(skill, timeline)), new ActMoveToward());

        public static BtNode MeleePuncher(SkillNodeId skill, TimelineId timeline)
        {
            var combat = CombatMelee(skill, timeline);
            return new BtSelector(
                new BtSequence(new CondHasTag(CommonTags.Dead), new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Downed), new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Stunned), new ActStopMove()),
                new BtSequence(new CondHasTarget(), combat.Clone()),
                new BtSequence(new ActAcquireHostile(), combat.Clone()),
                new ActStopMove());
        }

        public static BtNode MeleeGuard(SkillNodeId skill, TimelineId timeline)
        {
            var combat = CombatMelee(skill, timeline);
            return new BtSelector(
                new BtSequence(new CondHasTag(CommonTags.Dead), new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Downed), new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Stunned), new ActStopMove()),
                new BtSequence(new CondBeyondLeash(), new ActStartReturn(), new BtSelector(new ActHoldIfPlaying(), new ActMoveTowardHome())),
                new BtSequence(new CondHasTarget(), combat.Clone()),
                new BtSequence(new ActAcquireHostile(), combat.Clone()),
                new ActPatrol());
        }

        public static BtNode SummonMelee(SkillNodeId skill, TimelineId timeline)
        {
            var combat = CombatMelee(skill, timeline);
            return new BtSelector(
                new BtSequence(new CondHasTag(CommonTags.Dead), new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Downed), new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Stunned), new ActStopMove()),
                new BtSequence(new CondOwnerDead(), new ActStopMove(), new ActRequestDespawn()),
                new BtSequence(new CondHasTarget(), combat.Clone()),
                new BtSequence(new ActAcquireHostile(), combat.Clone()),
                new ActFollowOwner());
        }

        public static BtNode Ranged(SkillNodeId skill, TimelineId timeline)
        {
            var attack = new BtSequence(new CondInRange(), new ActStopMove(), new ActFaceTarget(), new ActPlaySkill(skill, timeline));
            return new BtSelector(
                new BtSequence(new CondHasTag(CommonTags.Dead), new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Downed), new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Stunned), new ActStopMove()),
                new BtSequence(new CondHasTarget(), attack),
                new BtSequence(new ActAcquireHostile(), attack.Clone()),
                new ActMoveToward());
        }
    }
}
