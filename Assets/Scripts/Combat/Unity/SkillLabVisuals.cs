using System.Collections.Generic;
using Combat.Config;
using UnityEngine;

namespace Combat.Unity
{
    // Snapshot-only view: Unity transforms never become combat input.
    public sealed class SkillLabVisuals : MonoBehaviour
    {
        readonly Dictionary<string, GameObject> _objects = new Dictionary<string, GameObject>();
        readonly List<Material> _materials = new List<Material>();
        SkillLabRunner _runner;
        Material _caster, _target, _projectile, _area,_obstacle,_summon;
        int _generation = -1;

        void Awake()
        {
            _runner = GetComponent<SkillLabRunner>();
            _caster = Material(new Color(.2f, .65f, .58f));
            _target = Material(new Color(.72f, .43f, .28f));
            _projectile = Material(new Color(1f, .85f, .5f));
            _area = Material(new Color(.3f, .35f, .55f));
            _obstacle=Material(new Color(.35f,.4f,.43f));
            _summon=Material(new Color(.24f,.8f,.44f));
        }

        Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new System.InvalidOperationException("SkillLab requires an Unlit shader.");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            _materials.Add(material);
            return material;
        }

        void LateUpdate()
        {
            if (_runner == null) return;
            if (_generation != _runner.Generation) { Clear(); _generation = _runner.Generation; }
            var snapshot = _runner.Snapshot;
            if (snapshot == null) return;
            var alive = new HashSet<string>();
            foreach (var actor in snapshot.Actors)
            {
                alive.Add(actor.Id);
                if (!_objects.TryGetValue(actor.Id, out var visual))
                {
                    visual = Create(actor); _objects.Add(actor.Id, visual);
                }
                visual.transform.position = new Vector3(actor.X,actor.Y+(actor.Kind == "aoe" ? .03f : .45f), actor.Z);
                float size = actor.Kind == "projectile" ? Mathf.Max(.12f, actor.Radius * 2) : Mathf.Max(.08f,actor.Radius*2);
                visual.transform.localScale = new Vector3(size, actor.Kind == "aoe" ? .02f : size, size);
            }
            foreach(var obstacle in snapshot.Obstacles)
            {
                string id="obstacle:"+obstacle.Id;alive.Add(id);
                if(!_objects.TryGetValue(id,out var visual))
                {
                    visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="Obstacle_"+obstacle.Id;visual.transform.SetParent(transform,false);
                    visual.GetComponent<Renderer>().sharedMaterial=_obstacle;Destroy(visual.GetComponent<Collider>());_objects.Add(id,visual);
                }
                visual.transform.position=new Vector3(obstacle.X,.4f,obstacle.Z);visual.transform.localScale=new Vector3(obstacle.Width,.8f,obstacle.Depth);
            }
            foreach(var ray in snapshot.Rays)
            {
                string id="ray:"+ray.Sequence;alive.Add(id);
                if(!_objects.TryGetValue(id,out var visual))
                {visual=new GameObject("Ray_"+ray.Sequence);visual.transform.SetParent(transform,false);var line=visual.AddComponent<LineRenderer>();line.positionCount=2;line.sharedMaterial=_projectile;_objects.Add(id,visual);}
                var renderer=visual.GetComponent<LineRenderer>();renderer.widthMultiplier=ray.Width;
                renderer.SetPosition(0,new Vector3(ray.FromX,.2f,ray.FromZ));renderer.SetPosition(1,new Vector3(ray.ToX,.2f,ray.ToZ));
            }
            foreach (var area in snapshot.Areas)
            {
                string id = "circle:" + area.Sequence; alive.Add(id);
                if (!_objects.TryGetValue(id, out var ring))
                {
                    ring = new GameObject("AreaPulse_" + area.Sequence); ring.transform.SetParent(transform, false);
                    var line = ring.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 65;
                    line.widthMultiplier = .04f; line.sharedMaterial = _projectile;
                    for (int i = 0; i <= 64; i++)
                    { float angle = i * Mathf.PI * 2 / 64; line.SetPosition(i, new Vector3(Mathf.Cos(angle)*area.Radius, 0, Mathf.Sin(angle)*area.Radius)); }
                    _objects.Add(id, ring);
                }
                ring.transform.position = new Vector3(area.X, .08f, area.Z);
            }
            foreach(var volume in snapshot.Volumes)
            {
                string id="volume:"+volume.Sequence;alive.Add(id);
                if(!_objects.TryGetValue(id,out var visual))
                {
                    visual=new GameObject("SkillVolume_"+volume.Shape);visual.transform.SetParent(transform,false);
                    var renderer=visual.AddComponent<LineRenderer>();renderer.useWorldSpace=false;renderer.widthMultiplier=.04f;renderer.sharedMaterial=_projectile;
                    _objects.Add(id,visual);
                }
                var points=SkillVolumeGeometry.Points(volume);var line=visual.GetComponent<LineRenderer>();line.positionCount=points.Length;line.SetPositions(points);
                visual.transform.position=new Vector3(volume.X,.08f,volume.Z);visual.transform.rotation=Quaternion.Euler(0,-volume.Yaw,0);
            }
            var removed = new List<string>();
            foreach (var item in _objects) if (!alive.Contains(item.Key)) removed.Add(item.Key);
            foreach (var id in removed) { Destroy(_objects[id]); _objects.Remove(id); }
        }

        GameObject Create(SkillActorSnapshot actor)
        {
            var visual = GameObject.CreatePrimitive(actor.Kind == "projectile" ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            visual.name = actor.Kind + "_" + actor.Name;
            visual.transform.SetParent(transform, false);
            visual.GetComponent<Renderer>().sharedMaterial = actor.Kind == "caster" ? _caster : actor.Kind == "projectile" ? _projectile : actor.Kind == "aoe" ? _area : actor.Kind=="summon"?_summon:_target;
            Destroy(visual.GetComponent<Collider>());
            return visual;
        }

        void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null || _runner?.Snapshot == null) return;
            foreach (var actor in _runner.Snapshot.Actors)
            {
                if (!actor.Hp.HasValue) continue;
                var point = camera.WorldToScreenPoint(new Vector3(actor.X, 1, actor.Z));
                GUI.Label(new Rect(point.x - 45, Screen.height - point.y - 20, 120, 40),
                    actor.Name + "\n" + actor.Hp.Value.ToString("F0") + " / " + actor.MaxHp.Value.ToString("F0"));
                if(actor.Buffs.Length>0)
                {
                    var names=new System.Collections.Generic.List<string>();foreach(var buff in actor.Buffs)names.Add(buff.Name+" x"+buff.Stacks);
                    GUI.Label(new Rect(point.x-45,Screen.height-point.y+17,200,30),string.Join(" / ",names));
                }
            }
        }

        void Clear() { foreach (var visual in _objects.Values) if (visual != null) Destroy(visual); _objects.Clear(); }
        void OnDestroy() { Clear(); foreach (var material in _materials) if (material != null) Destroy(material); }
    }
}
