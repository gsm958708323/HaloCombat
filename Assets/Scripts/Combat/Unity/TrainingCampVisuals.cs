using System.Collections.Generic;
using Combat.Core;
using UnityEngine;

namespace Combat.TrainingCamp
{
    public sealed class TrainingCampVisuals : MonoBehaviour
    {
        TrainingCampRunner _runner;
        readonly Dictionary<EntityId, GameObject> _objects = new Dictionary<EntityId, GameObject>();
        readonly Dictionary<EntityId, LineRenderer> _links = new Dictionary<EntityId, LineRenderer>();
        Transform _root;
        Material _fighter, _dummy, _summon, _projectile, _aoe;

        void Awake()
        {
            _runner = GetComponent<TrainingCampRunner>();
            _root = new GameObject("TrainingCampVisuals").transform;
            _root.SetParent(transform);
            _fighter = Mat(new Color(0.08f, .75f, 1f)); _dummy = Mat(new Color(1f, .28f, .08f));
            _summon = Mat(new Color(.1f, 1f, .65f)); _projectile = Mat(new Color(1f, .65f, .08f)); _aoe = Mat(new Color(.35f, .2f, 1f, .45f));
        }
        Material Mat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[TrainingCamp] No compatible runtime shader was found.");
                return null;
            }
            var m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 1.5f);
            }
            return m;
        }
        void LateUpdate()
        {
            if (_runner == null || _runner.World == null) return;
            var alive = new HashSet<EntityId>();
            foreach (var actor in _runner.World.RegistryActive())
            {
                alive.Add(actor.Id); if (!actor.TryGetComp<TransformComp>(out var tf)) continue;
                if (!_objects.TryGetValue(actor.Id, out var go)) { go = Make(actor); _objects.Add(actor.Id, go); }
                go.transform.position = new Vector3(tf.Position.X, tf.Position.Y + .65f, tf.Position.Z);
                go.transform.rotation = Quaternion.Euler(0f, -tf.YawDegrees, 0f);
                if (actor.TryGetComp<AoeComp>(out var aoe) && aoe.Def != null) go.transform.localScale = new Vector3(aoe.Def.Radius * 2f, .05f, aoe.Def.Radius * 2f);
                UpdateLink(actor, tf.Position);
            }
            var dead = new List<EntityId>(); foreach (var kv in _objects) if (!alive.Contains(kv.Key)) dead.Add(kv.Key);
            foreach (var id in dead) { Destroy(_objects[id]); _objects.Remove(id); if (_links.TryGetValue(id, out var link)) { Destroy(link.gameObject); _links.Remove(id); } }
        }
        GameObject Make(Actor actor)
        {
            PrimitiveType type = PrimitiveType.Capsule; Material material = _fighter;
            if (actor.Id == _runner.DummyId) material = _dummy;
            if (actor.TryGetComp<SummonComp>(out _)) material = _summon;
            if (actor.TryGetComp<ProjectileComp>(out _)) { type = PrimitiveType.Sphere; material = _projectile; }
            if (actor.TryGetComp<AoeComp>(out _)) { type = PrimitiveType.Cylinder; material = _aoe; }
            var go = GameObject.CreatePrimitive(type); go.name = "camp_actor_" + actor.Id.Index; go.transform.SetParent(_root);
            go.GetComponent<Renderer>().sharedMaterial = material; var collider = go.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "status_ring"; ring.transform.SetParent(go.transform); ring.transform.localPosition = new Vector3(0f, -.55f, 0f); ring.transform.localScale = new Vector3(1.25f, .025f, 1.25f); ring.GetComponent<Renderer>().sharedMaterial = material; var ringCollider = ring.GetComponent<Collider>(); if (ringCollider != null) Destroy(ringCollider);
            if (actor.TryGetComp<ProjectileComp>(out _)) { var trail = go.AddComponent<TrailRenderer>(); trail.time = .25f; trail.startWidth = .15f; trail.endWidth = 0f; trail.material = material; }
            return go;
        }
        void UpdateLink(Actor actor, SimVec3 from)
        {
            EntityId target = EntityId.Invalid;
            if (actor.TryGetComp<SummonComp>(out var summon)) target = summon.OwnerId;
            else if (actor.TryGetComp<ProjectileComp>(out var projectile)) target = projectile.HomingTarget;
            if (!target.IsValid || !_runner.World.TryGetActor(target, out var targetActor) || !targetActor.TryGetComp<TransformComp>(out var targetTf))
            {
                if (_links.TryGetValue(actor.Id, out var old)) old.enabled = false;
                return;
            }
            if (!_links.TryGetValue(actor.Id, out var line))
            {
                var go = new GameObject("owner_link_" + actor.Id.Index); go.transform.SetParent(_root); line = go.AddComponent<LineRenderer>(); line.positionCount = 2; line.widthMultiplier = .025f; line.material = _summon; _links.Add(actor.Id, line);
            }
            line.enabled = true; line.SetPosition(0, new Vector3(from.X, from.Y + .65f, from.Z)); line.SetPosition(1, new Vector3(targetTf.Position.X, targetTf.Position.Y + .65f, targetTf.Position.Z));
        }
        void OnDestroy() { foreach (var go in _objects.Values) if (go != null) Destroy(go); foreach (var line in _links.Values) if (line != null) Destroy(line.gameObject); }
    }
}
