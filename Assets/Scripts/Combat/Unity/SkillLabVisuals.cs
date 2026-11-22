using System.Collections.Generic;
using System.Linq;
using Combat.Config;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    // Snapshot-only view: Unity transforms never become combat input.
    public sealed class SkillLabVisuals : MonoBehaviour
    {
        readonly Dictionary<string, GameObject> _objects = new Dictionary<string, GameObject>();
        readonly List<Material> _materials = new List<Material>();
        readonly Dictionary<string,UnityBodyVisual> _bodies=new Dictionary<string,UnityBodyVisual>();
        readonly SkillLabDamageNumbers _damageNumbers=new SkillLabDamageNumbers();
        public int ActiveDamageNumbers=>_damageNumbers.Count;
        SkillLabRunner _runner;
        Material _caster, _target, _projectile, _area,_obstacle,_summon,_construct;
        Material _range, _aimMaterial, _trail;
        SkillLabStage _stage;
        int _generation = -1;

        public void ResetView() => _stage?.ResetView();

        void Awake()
        {
            _runner = GetComponent<SkillLabRunner>();
            _caster = Material(new Color(.2f, .65f, .58f), true);
            _target = Material(new Color(.72f, .43f, .28f), true);
            _projectile = Material(new Color(1f, .85f, .5f));
            _area = Material(new Color(.3f, .35f, .55f));
            _obstacle=Material(new Color(.35f,.4f,.43f), true);
            _summon=Material(new Color(.24f,.8f,.44f), true);
            _construct=Material(new Color(.65f,.3f,.1f), true);
            _range = Material(new Color(.95f, .66f, .35f));
            _aimMaterial = Material(new Color(.49f, .87f, .88f));
            _trail = Material(new Color(.65f, .73f, .92f));
            _stage = new SkillLabStage(Camera.main, transform, Material(new Color(.095f, .14f, .17f)),
                Material(new Color(.19f, .26f, .30f)));
        }

        Material Material(Color color, bool lit = false)
        {
            var shader = lit ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new System.InvalidOperationException("SkillLab requires an Unlit shader.");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            _materials.Add(material);
            return material;
        }

        void LateUpdate()
        {
            if (_runner == null) return;
            var snapshot = _runner.Snapshot;
            if (snapshot == null) return;
            if (_generation != _runner.Generation)
            {
                Clear();
                _generation = _runner.Generation;
                _stage.Build(snapshot);
            }
            _stage.UpdateView(SkillLabHud.PointerOver ? 0 : Input.mouseScrollDelta.y);
            _damageNumbers.Update(snapshot, _runner.ShowDamageNumbers);
            var alive = new HashSet<string>();
            foreach (var actor in snapshot.Actors)
            {
                if (actor.Kind == "aoe" && actor.InnerRadius > 0)
                {
                    if (_runner.ShowRanges)
                    {
                        DrawGroundOutline(actor.Id + ":inner", actor.X, actor.Z, actor.InnerRadius.Value, alive);
                        DrawGroundOutline(actor.Id + ":outer", actor.X, actor.Z, actor.Radius, alive);
                    }
                    if (actor.View == null) continue;
                }
                if (actor.Kind == "aoe" && actor.View == null && !_runner.ShowRanges) continue;
                if (actor.ReturnAnchors != null)
                    foreach (var anchor in actor.ReturnAnchors)
                        DrawGroundOutline($"anchor:{actor.Id}:{anchor.CastId}", anchor.X, anchor.Z, .42f, alive);
                alive.Add(actor.Id);
                if (!_objects.TryGetValue(actor.Id, out var visual))
                {
                    visual = Create(actor); _objects.Add(actor.Id, visual);
                }
                if(_bodies.TryGetValue(actor.Id,out var body))
                {
                    visual.transform.position=new Vector3(actor.X,actor.Y,actor.Z);visual.transform.rotation=Quaternion.Euler(0,90-actor.Yaw,0);
                    body.Sample(new PoseSample{ActorTime=(float)actor.LogicalTime,BodyScale=actor.ViewScale,InHitstop=actor.Stopped,PresentationPaused=_runner.Paused});continue;
                }
                float size = actor.Kind == "projectile" ? Mathf.Max(.12f, actor.Radius * 2) : Mathf.Max(.08f,actor.Radius*2);
                bool raisedBody = actor.Kind != "aoe" && actor.Kind != "projectile";
                float height = raisedBody ? Mathf.Max(.5f, size * 1.45f) : actor.Kind == "aoe" ? .02f : size;
                visual.transform.position = new Vector3(actor.X, actor.Y + (actor.Kind == "projectile" ? .45f : height * .5f), actor.Z);
                visual.transform.localScale = new Vector3(size, raisedBody ? height * .5f : height, size);
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
            if (_runner.ShowRanges) DrawRanges(snapshot, alive);
            if (_runner.ShowTrajectories) DrawTrails(snapshot, alive);
            var removed = new List<string>();
            foreach (var item in _objects) if (!alive.Contains(item.Key)) removed.Add(item.Key);
            foreach (var id in removed) { Destroy(_objects[id]); _objects.Remove(id);_bodies.Remove(id); }
        }

        void DrawGroundOutline(string id, float x, float z, float radius, HashSet<string> alive)
        {
            alive.Add(id);
            if (!_objects.TryGetValue(id, out var visual))
            {
                var line=CombatGroundOutline.Create(transform, _aimMaterial, id);
                visual=line.gameObject;
                _objects.Add(id, visual);
            }
            CombatGroundOutline.SetRadius(visual.GetComponent<LineRenderer>(), radius);
            visual.transform.position=new Vector3(x, 0, z);
        }

        void DrawRanges(SkillSimulationSnapshot snapshot, HashSet<string> alive)
        {
            foreach(var ray in snapshot.Rays)
            {
                if (snapshot.Step - ray.Step > 18) continue;
                string id="ray:"+ray.Sequence;alive.Add(id);
                if(!_objects.TryGetValue(id,out var visual))
                {visual=new GameObject("Ray_"+ray.Sequence);visual.transform.SetParent(transform,false);var line=visual.AddComponent<LineRenderer>();line.positionCount=2;line.sharedMaterial=_projectile;_objects.Add(id,visual);}
                var renderer=visual.GetComponent<LineRenderer>();renderer.widthMultiplier=ray.Width;
                renderer.SetPosition(0,new Vector3(ray.FromX,.2f,ray.FromZ));renderer.SetPosition(1,new Vector3(ray.ToX,.2f,ray.ToZ));
            }
            foreach (var area in snapshot.Areas)
            {
                if (snapshot.Step - area.Step > 26) continue;
                string id = "circle:" + area.Sequence; alive.Add(id);
                if (!_objects.TryGetValue(id, out var ring))
                {
                    ring = new GameObject("AreaPulse_" + area.Sequence); ring.transform.SetParent(transform, false);
                    var line = ring.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.positionCount = 65;
                    line.widthMultiplier = .04f; line.sharedMaterial = _range;
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
                    var renderer=visual.AddComponent<LineRenderer>();renderer.useWorldSpace=false;renderer.widthMultiplier=.04f;renderer.sharedMaterial=_range;
                    _objects.Add(id,visual);
                }
                var points=SkillVolumeGeometry.Points(volume);var line=visual.GetComponent<LineRenderer>();line.positionCount=points.Length;line.SetPositions(points);
                SkillLabSweepVisual.Update(visual,volume,_range);
                visual.transform.position=new Vector3(volume.X,.08f,volume.Z);visual.transform.rotation=Quaternion.Euler(0,-volume.Yaw,0);
            }
            DrawAim(snapshot, alive);
        }

        void DrawAim(SkillSimulationSnapshot snapshot, HashSet<string> alive)
        {
            var aim = _runner.AimPreview;
            if (aim == null || !_runner.PointerInWorld) return;
            if (aim.SearchRadius > 0)
            {
                var caster = snapshot.Actors.FirstOrDefault(actor => actor.Kind == "caster");
                if (caster != null)
                {
                    var line = Overlay("aim-range", alive, _aimMaterial);
                    line.positionCount = 65;
                    for (int i = 0; i <= 64; i++)
                    {
                        float angle = i * Mathf.PI * 2 / 64;
                        line.SetPosition(i, new Vector3(caster.X + Mathf.Cos(angle) * aim.SearchRadius, .025f,
                            caster.Z + Mathf.Sin(angle) * aim.SearchRadius));
                    }
                }
            }
            if (!aim.HasTarget || aim.TargetMode == "random" || aim.TargetMode == "direction" || aim.TargetMode == "facing") return;
            var marker = Overlay("aim-point", alive, _aimMaterial);
            marker.positionCount = 5;
            marker.SetPosition(0, new Vector3(aim.X - .25f, .08f, aim.Z));
            marker.SetPosition(1, new Vector3(aim.X, .08f, aim.Z + .25f));
            marker.SetPosition(2, new Vector3(aim.X + .25f, .08f, aim.Z));
            marker.SetPosition(3, new Vector3(aim.X, .08f, aim.Z - .25f));
            marker.SetPosition(4, new Vector3(aim.X - .25f, .08f, aim.Z));
        }

        void DrawTrails(SkillSimulationSnapshot snapshot, HashSet<string> alive)
        {
            foreach (var trail in snapshot.Trails.Where(point => snapshot.Step - point.Step <= 18).GroupBy(point => point.Entity).Take(48))
            {
                var points = trail.Select(point => new Vector3(point.X, .07f, point.Z)).ToArray();
                if (points.Length < 2) continue;
                var line = Overlay("trail:" + trail.Key, alive, _trail);
                line.positionCount = points.Length;
                line.SetPositions(points);
            }
        }

        LineRenderer Overlay(string id, HashSet<string> alive, Material material)
        {
            alive.Add(id);
            if (_objects.TryGetValue(id, out var existing)) return existing.GetComponent<LineRenderer>();
            var visual = new GameObject(id);
            visual.transform.SetParent(transform, false);
            var line = visual.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = .032f;
            _objects.Add(id, visual);
            return line;
        }

        GameObject Create(SkillActorSnapshot actor)
        {
            if(actor.View!=null&&_runner.Views!=null&&_runner.Views.TryGetValue(actor.View,out var definition))
            {
                var root=new GameObject(actor.Kind+"_"+actor.Name);root.transform.SetParent(transform,false);var visualRoot=new GameObject("Visual").transform;visualRoot.SetParent(root.transform,false);
                var instance=Instantiate(Resources.Load<GameObject>(definition.Resource),visualRoot,false);instance.transform.localRotation=Quaternion.Euler(0,definition.YawOffset,0);instance.transform.localScale*=definition.Scale;
                _bodies.Add(actor.Id,new UnityBodyVisual(instance,definition,visualRoot));return root;
            }
            var visual = GameObject.CreatePrimitive(actor.Kind == "projectile" ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
            visual.name = actor.Kind + "_" + actor.Name;
            visual.transform.SetParent(transform, false);
            visual.GetComponent<Renderer>().sharedMaterial = actor.Kind == "caster" ? _caster : actor.Kind == "projectile" ? _projectile : actor.Kind == "aoe" ? _area : actor.Kind=="summon"?_summon:actor.Kind=="construct"?_construct:_target;
            Destroy(visual.GetComponent<Collider>());
            return visual;
        }

        void OnGUI()
        {
            var camera = Camera.main;
            if (camera == null || _runner?.Snapshot == null) return;
            if (_runner.ShowDamageNumbers) _damageNumbers.Draw(camera);
            foreach (var actor in _runner.Snapshot.Actors)
            {
                if (!actor.Hp.HasValue) continue;
                var point = camera.WorldToScreenPoint(new Vector3(actor.X, 1, actor.Z));
                if (point.z < 0 || SkillLabHud.Bounds.Contains(new Vector2(point.x, Screen.height - point.y))) continue;
                GUI.Label(new Rect(point.x - 45, Screen.height - point.y - 20, 120, 40),
                    actor.Name + "\n" + actor.Hp.Value.ToString("F0") + " / " + actor.MaxHp.Value.ToString("F0"));
                if(actor.Buffs.Length>0)
                {
                    var names=new System.Collections.Generic.List<string>();foreach(var buff in actor.Buffs)names.Add(buff.Name + " ×" + buff.Stacks + (buff.Remaining.HasValue ? $" · {buff.Remaining:0.0}s" : ""));
                    GUI.Label(new Rect(point.x-45,Screen.height-point.y+17,200,30),string.Join(" / ",names));
                }
            }
        }

        void Clear() { foreach (var visual in _objects.Values) if (visual != null) Destroy(visual); _objects.Clear();_bodies.Clear();_damageNumbers.Clear(); }
        void OnDestroy() { Clear(); _stage?.Clear(); foreach (var material in _materials) if (material != null) Destroy(material); }
    }
}
