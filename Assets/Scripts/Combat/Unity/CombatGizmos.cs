using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    [RequireComponent(typeof(CombatRunner))]
    public sealed class CombatGizmos : MonoBehaviour
    {
        CombatRunner _runner;

        void Awake() => _runner = GetComponent<CombatRunner>();

        void OnDrawGizmos()
        {
            if (_runner == null || _runner.World == null) return;
            var world = _runner.World;
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (!actor.TryGetComp<TransformComp>(out var transform)) continue;
                var position = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                if (actor.TryGetComp<HitboxComp>(out var hitbox) && hitbox.IsOpen)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireSphere(position, hitbox.Radius);
                }

                if (actor.TryGetComp<ProjectileComp>(out var projectile) && projectile.Def != null)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawWireSphere(position, projectile.Def.HitRadius);
                    if (projectile.HomingTarget.IsValid && world.TryGetActor(projectile.HomingTarget, out var target) && target.TryGetComp<TransformComp>(out var targetTransform))
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(position, new Vector3(targetTransform.Position.X, targetTransform.Position.Y, targetTransform.Position.Z));
                    }
                }

                if (actor.TryGetComp<AoeComp>(out var aoe) && aoe.Def != null)
                {
                    Gizmos.color = aoe.Def.TrackOccupancy ? Color.cyan : new Color(1f, 0.5f, 0f);
                    Gizmos.DrawWireSphere(position, aoe.Def.Radius);
                }

                if (actor.TryGetComp<BehaviorTreeComp>(out var bt))
                {
                    var home = bt.Board.Home;
                    Gizmos.color = Color.yellow;
                    Gizmos.DrawWireSphere(new Vector3(home.X, home.Y, home.Z), bt.Board.LeashRange > 0.1f ? bt.Board.LeashRange : 0.2f);
                    Gizmos.DrawSphere(new Vector3(home.X, home.Y, home.Z), 0.08f);
                }

                if (actor.TryGetComp<SummonComp>(out var summon) && world.TryGetActor(summon.OwnerId, out var owner) && owner.TryGetComp<TransformComp>(out var ownerTransform))
                {
                    Gizmos.color = Color.cyan;
                    var ownerPosition = new Vector3(ownerTransform.Position.X, ownerTransform.Position.Y, ownerTransform.Position.Z);
                    Gizmos.DrawWireSphere(ownerPosition, bt != null ? bt.Board.FollowRange : 2f);
                }
            }
        }
    }
}
