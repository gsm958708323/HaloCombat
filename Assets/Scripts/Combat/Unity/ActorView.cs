using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class ActorView : MonoBehaviour
    {
        public EntityId Id;
        public bool IsProjectile;
        public bool IsPulse;

        public void Sync(DemoCombatSession session)
        {
            if (!session.World.TryGetActor(Id, out var actor))
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            var tf = actor.GetComp<TransformComp>();
            transform.position = new Vector3(tf.Position.X, tf.Position.Y, tf.Position.Z);
            // Core yaw uses +X as forward; Unity primitives use +Z as forward.
            transform.rotation = Quaternion.Euler(0f, 90f - tf.YawDegrees, 0f);

            var rend = GetComponentInChildren<Renderer>();
            if (rend == null) return;

            if (IsPulse)
            {
                rend.material.color = new Color(1f, 0.4f, 0.1f, 0.55f);
                return;
            }
            if (IsProjectile)
            {
                rend.material.color = Color.yellow;
                return;
            }

            if (actor.TryGetComp<TeamComp>(out var team) && team.Team == 0)
            {
                var fsm = actor.GetComp<StateMachineComp>();
                rend.material.color = fsm.Current == ActorStateId.Attack ? Color.cyan
                    : fsm.Current == ActorStateId.Hit ? Color.magenta
                    : fsm.Current == ActorStateId.Dead ? Color.black
                    : fsm.Current == ActorStateId.Jump ? Color.white
                    : Color.green;
            }
            else if (actor.TryGetComp<HealthComp>(out var hp))
            {
                float t = hp.MaxHp > 0 ? hp.Hp / hp.MaxHp : 0f;
                rend.material.color = Color.Lerp(Color.red, new Color(0.6f, 0.2f, 0.2f), 1f - t);
                if (hp.IsDead) rend.material.color = Color.gray;
            }
        }
    }

    public static class ActorViewSpawner
    {
        static readonly System.Collections.Generic.Dictionary<int, ActorView> Map =
            new System.Collections.Generic.Dictionary<int, ActorView>();

        public static void SpawnAll(DemoCombatSession session)
        {
            Map.Clear();
            EnsureView(session, session.PlayerId, PrimitiveType.Capsule, 0.9f, false, false);
            EnsureView(session, session.DummyMeleeId, PrimitiveType.Cube, 0.8f, false, false);
            EnsureView(session, session.DummyRangedId, PrimitiveType.Cube, 0.8f, false, false);
        }

        public static void SyncAll(DemoCombatSession session)
        {
            // 动态投射物 / 火池
            foreach (var a in session.World.CopyActiveActors())
            {
                bool proj = a.TryGetComp<ProjectileMoveComp>(out _);
                bool pulse = a.TryGetComp<PulseZoneComp>(out _);
                if (!proj && !pulse && a.Id != session.PlayerId &&
                    a.Id != session.DummyMeleeId && a.Id != session.DummyRangedId)
                    continue;

                if (!Map.ContainsKey(a.Id.Index))
                {
                    var type = pulse ? PrimitiveType.Cylinder : PrimitiveType.Sphere;
                    float scale = pulse ? 2.4f : 0.35f;
                    EnsureView(session, a.Id, type, scale, proj, pulse);
                }
            }

            var dead = new System.Collections.Generic.List<int>();
            foreach (var kv in Map)
            {
                if (!session.World.TryGetActor(kv.Value.Id, out _))
                {
                    Object.Destroy(kv.Value.gameObject);
                    dead.Add(kv.Key);
                }
                else kv.Value.Sync(session);
            }
            foreach (var k in dead) Map.Remove(k);
        }

        static void EnsureView(
            DemoCombatSession session,
            EntityId id,
            PrimitiveType type,
            float scale,
            bool proj,
            bool pulse)
        {
            if (Map.ContainsKey(id.Index)) return;
            var go = GameObject.CreatePrimitive(type);
            go.name = $"View_{id}";
            go.transform.localScale = Vector3.one * scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // 逻辑碰撞不走 PhysX

            var view = go.AddComponent<ActorView>();
            view.Id = id;
            view.IsProjectile = proj;
            view.IsPulse = pulse;
            Map[id.Index] = view;
            view.Sync(session);
        }
    }
}
