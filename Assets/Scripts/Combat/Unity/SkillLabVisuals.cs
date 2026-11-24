using System.Collections.Generic;
using System.Linq;
using Combat.Config;
using Combat.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace Combat.Unity
{
    // Snapshot-only view: Unity transforms never become combat input.
    public sealed class SkillLabVisuals : MonoBehaviour
    {
        readonly Dictionary<string, GameObject> _objects = new Dictionary<string, GameObject>();
        readonly List<Material> _materials = new List<Material>();
        readonly Dictionary<string,UnityBodyVisual> _bodies=new Dictionary<string,UnityBodyVisual>();
        readonly SkillLabDamageNumbers _damageNumbers=new SkillLabDamageNumbers();
        readonly Dictionary<string, SkillLabActorDisplay> _actorDisplays = new Dictionary<string, SkillLabActorDisplay>();
        readonly HashSet<string> _alive = new HashSet<string>();
        readonly List<string> _removed = new List<string>();
        readonly Dictionary<string, Rect> _placedActorLabels = new Dictionary<string, Rect>();
        SkillLabFeedback _feedback;
        GUIStyle _actorLabel, _buffLabel;
        public int ActiveDamageNumbers => _damageNumbers.Count;
        internal int MetricsGeneration => _generation;
        internal float DamagePerSecond => _feedback?.DamagePerSecond ?? 0;
        internal bool DamageWindowIncomplete => _feedback?.DamageWindowIncomplete == true;
        internal bool Muted { get => _feedback?.Muted == true; set { if (_feedback != null) _feedback.Muted = value; } }
        SkillLabRunner _runner;
        Material _caster, _target, _projectile, _area,_obstacle,_summon,_construct;
        Material _range, _aimMaterial, _trail, _woodGrain, _band, _wake, _targetFoot, _casterFoot;
        SkillLabStage _stage;
        int _generation = -1;

        public void ResetView() => _stage?.ResetView();

        void Awake()
        {
            _runner = GetComponent<SkillLabRunner>();
            _caster = Material(new Color32(49, 122, 136, 255), true);
            _target = Material(new Color32(116, 70, 46, 255), true);
            _woodGrain = Material(new Color32(173, 111, 65, 255), true);
            _band = Material(new Color32(67, 71, 70, 255), true);
            _targetFoot = Material(new Color32(127, 102, 74, 255));
            _casterFoot = Material(new Color32(69, 201, 189, 255));
            _projectile = Material(new Color(1f, .85f, .5f));
            _area = Material(new Color(.3f, .35f, .55f));
            _obstacle=Material(new Color(.35f,.4f,.43f), true);
            _summon=Material(new Color(.24f,.8f,.44f), true);
            _construct=Material(new Color(.65f,.3f,.1f), true);
            _range = Material(new Color(.95f, .66f, .35f));
            _aimMaterial = Material(new Color(.49f, .87f, .88f));
            _trail = Material(new Color(.65f, .73f, .92f));
            var particleLine = Shader.Find("Sprites/Default");
            _wake = particleLine == null ? _trail : new Material(particleLine) { name = "SkillLab projectile wake" };
            if (_wake != _trail) _materials.Add(_wake);
            _stage = new SkillLabStage(Camera.main, transform, Material(new Color32(46, 51, 56, 255), true),
                Material(new Color32(65, 73, 80, 255)), Material(new Color32(89, 101, 109, 255)));
            _feedback = new SkillLabFeedback(transform, _aimMaterial, _damageNumbers);
        }

        Material Material(Color color, bool lit = false)
        {
            var shader = lit ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) throw new System.InvalidOperationException("SkillLab requires an Unlit shader.");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .18f);
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
                _feedback.Reset();
            }
            _stage.UpdateView(SkillLabHud.PointerOver ? 0 : Input.mouseScrollDelta.y);
            _feedback.Update(snapshot, _runner.Paused || _runner.ReplayMode, _runner.ShowDamageNumbers);
            _alive.Clear();
            var alive = _alive;
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
                if ((actor.Kind == "target" || actor.Kind == "caster") && !actor.Dead)
                {
                    bool caster = actor.Kind == "caster";
                    DrawGroundOutline($"foot:{actor.Id}", actor.X, actor.Z, Mathf.Max(.23f, actor.Radius + .06f), alive,
                        caster ? _casterFoot : _targetFoot);
                }
                alive.Add(actor.Id);
                if (!_objects.TryGetValue(actor.Id, out var visual))
                {
                    visual = Create(actor); _objects.Add(actor.Id, visual);
                }
                if (_bodies.TryGetValue(actor.Id, out var body))
                {
                    visual.transform.position = new Vector3(actor.X, actor.Y, actor.Z);
                    visual.transform.rotation = Quaternion.Euler(0, 90 - actor.Yaw, 0);
                    body.Sample(new PoseSample
                    {
                        ActorTime = (float)actor.LogicalTime,
                        BodyScale = actor.ViewScale,
                        InHitstop = actor.Stopped,
                        PresentationPaused = _runner.Paused
                    });
                    continue;
                }
                if (_actorDisplays.TryGetValue(actor.Id, out var display))
                {
                    visual.transform.position = new Vector3(actor.X, actor.Y, actor.Z);
                    visual.transform.rotation = actor.Kind == "caster" ? Quaternion.Euler(0, 90 - actor.Yaw, 0)
                        : actor.Dead ? Quaternion.Euler(0, 0, 68) : Quaternion.identity;
                    visual.transform.localScale = Vector3.one * SkillLabActorDisplay.Scale(actor);
                    display.Sample(_feedback.Flash(actor.Id));
                    continue;
                }
                float size = actor.Kind == "projectile" ? Mathf.Max(.12f, actor.Radius * 2) : Mathf.Max(.08f,actor.Radius*2);
                bool raisedBody = actor.Kind != "aoe" && actor.Kind != "projectile";
                float height = raisedBody ? Mathf.Max(.5f, size * 1.45f) : actor.Kind == "aoe" ? .02f : size;
                visual.transform.position = new Vector3(actor.X, actor.Y + (actor.Kind == "projectile" ? .45f : height * .5f), actor.Z);
                visual.transform.localScale = new Vector3(size, raisedBody ? height * .5f : height, size);
            }
            foreach (var obstacle in snapshot.Obstacles)
            {
                string id = "obstacle:" + obstacle.Id;
                alive.Add(id);
                if (_objects.ContainsKey(id)) continue;
                var center = new Vector3(obstacle.X, .4f, obstacle.Z);
                var scale = new Vector3(obstacle.Width, .8f, obstacle.Depth);
                var visual = SkillLabStage.Primitive(PrimitiveType.Cube, transform, "Obstacle_" + obstacle.Id, _obstacle, center, scale);
                _objects.Add(id, visual);
            }
            if (_runner.ShowRanges) DrawRanges(snapshot, alive);
            DrawAim(snapshot, alive);
            if (_runner.ShowTrajectories) DrawTrails(snapshot, alive);
            _removed.Clear();
            foreach (var item in _objects)
                if (!alive.Contains(item.Key)) _removed.Add(item.Key);
            foreach (var id in _removed)
            {
                Destroy(_objects[id]);
                _objects.Remove(id);
                _bodies.Remove(id);
                _actorDisplays.Remove(id);
            }
        }

        void DrawGroundOutline(string id, float x, float z, float radius, HashSet<string> alive, Material material = null)
        {
            alive.Add(id);
            if (!_objects.TryGetValue(id, out var visual))
            {
                var line=CombatGroundOutline.Create(transform, material ?? _aimMaterial, id);
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
        }

        void DrawAim(SkillSimulationSnapshot snapshot, HashSet<string> alive)
        {
            var aim = _runner.AimPreview;
            if (aim == null || !_runner.PointerInWorld || _runner.ReplayMode) return;
            if (_runner.ShowRanges && aim.SearchRadius > 0)
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
            if (aim.TargetMode == "random" || aim.TargetMode == "facing") return;
            if (aim.HasTarget || aim.TargetMode == "direction")
                DrawGroundOutline("aim-point", aim.X, aim.Z, .32f, alive);
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
            var root = new GameObject(actor.Kind + "_" + actor.Name);
            root.transform.SetParent(transform, false);
            // Start the wake at the current position rather than at the scene origin.
            root.transform.position = new Vector3(actor.X, actor.Y + (actor.Kind == "projectile" && actor.View == null ? .45f : 0), actor.Z);
            if (actor.View != null && _runner.Views != null && _runner.Views.TryGetValue(actor.View, out var definition))
            {
                var visualRoot = new GameObject("Visual").transform;
                visualRoot.SetParent(root.transform, false);
                var instance = Instantiate(Resources.Load<GameObject>(definition.Resource), visualRoot, false);
                instance.transform.localRotation = Quaternion.Euler(0, definition.YawOffset, 0);
                instance.transform.localScale *= definition.Scale;
                _bodies.Add(actor.Id, new UnityBodyVisual(instance, definition, visualRoot));
            }
            else if (actor.Kind == "caster" || actor.Kind == "target")
            {
                if (actor.Kind == "target") SkillLabActorDisplay.BuildDummy(root.transform, _target, _woodGrain, _band);
                else SkillLabActorDisplay.BuildCaster(root.transform, _caster, _band, _aimMaterial);
                _actorDisplays.Add(actor.Id, new SkillLabActorDisplay(root));
            }
            else
            {
                var material = actor.Kind switch
                {
                    "projectile" => _projectile,
                    "aoe" => _area,
                    "summon" => _summon,
                    "construct" => _construct,
                    _ => _target
                };
                SkillLabStage.Primitive(actor.Kind == "projectile" ? PrimitiveType.Sphere : PrimitiveType.Cylinder,
                    root.transform, "Body", material, Vector3.zero, Vector3.one);
            }
            if (actor.Kind == "projectile") AddWake(root, actor.Radius);
            return root;
        }

        void AddWake(GameObject root, float radius)
        {
            var wake = root.AddComponent<TrailRenderer>();
            wake.sharedMaterial = _wake;
            wake.time = .16f;
            wake.minVertexDistance = .12f;
            wake.startWidth = Mathf.Clamp(radius * .8f, .07f, .30f);
            wake.endWidth = 0;
            wake.startColor = new Color(.50f, .94f, 1f, .83f);
            wake.endColor = new Color(.22f, .61f, .9f, 0);
            wake.shadowCastingMode = ShadowCastingMode.Off;
            wake.receiveShadows = false;
            wake.Clear();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var camera = Camera.main;
            var state = _runner?.Snapshot;
            if (camera == null || state == null) return;
            _actorLabel ??= SkillLabHud.Text(14, new Color32(240, 246, 250, 255), true);
            _buffLabel ??= SkillLabHud.Text(13, new Color32(178, 225, 222, 255));
            _actorLabel.alignment = _buffLabel.alignment = TextAnchor.MiddleCenter;
            _actorLabel.wordWrap = _buffLabel.wordWrap = false;
            float scale = Mathf.Clamp(Screen.height / 720f, .8f, 1.6f);
            _actorLabel.fontSize = Mathf.RoundToInt(14 * scale);
            _buffLabel.fontSize = Mathf.RoundToInt(13 * scale);
            _placedActorLabels.Clear();
            foreach (var actor in state.Actors)
            {
                if (!actor.Hp.HasValue || !actor.MaxHp.HasValue || actor.MaxHp.Value <= 0 || actor.Dead) continue;
                float height = actor.View == null && (actor.Kind == "caster" || actor.Kind == "target")
                    ? 1.65f * SkillLabActorDisplay.Scale(actor) : 1.4f;
                var point = camera.WorldToScreenPoint(new Vector3(actor.X, actor.Y + height, actor.Z));
                var screen = new Vector2(point.x, Screen.height - point.y);
                if (point.z <= 0 || !new Rect(0, 0, Screen.width, Screen.height).Contains(screen) || SkillLabHud.Covers(screen)) continue;
                DrawActorLabel(actor, screen, scale);
            }
            if (_runner.ShowDamageNumbers) _damageNumbers.Draw(camera, _placedActorLabels);
        }

        void DrawActorLabel(SkillActorSnapshot actor, Vector2 anchor, float scale)
        {
            var name = new GUIContent(actor.Name);
            var buffs = actor.Buffs.Length == 0 ? null : new GUIContent(string.Join(" / ", actor.Buffs.Take(3).Select(buff =>
                buff.Name + " ×" + buff.Stacks + (buff.Remaining.HasValue ? $" · {buff.Remaining:0.0}s" : ""))));
            float textWidth = Mathf.Max(_actorLabel.CalcSize(name).x, buffs == null ? 0 : _buffLabel.CalcSize(buffs).x);
            float width = Mathf.Clamp(textWidth + 4 * scale, 64 * scale, 170 * scale);
            float height = (buffs == null ? 28 : 49) * scale;
            var preferred = new Rect(anchor.x - width / 2, anchor.y - 26 * scale, width, height);
            if (!TryPlaceActorLabel(preferred, 3 * scale, out var block)) return;
            _placedActorLabels.Add(actor.Id, block);

            if (block.y != preferred.y)
            {
                // A leader keeps the displaced readout associated with its projected target.
                float edge = block.y < preferred.y ? block.yMax : block.yMin;
                SkillLabHud.Fill(new Rect(anchor.x - .5f, Mathf.Min(anchor.y, edge), 1f, Mathf.Abs(anchor.y - edge)),
                    new Color32(194, 217, 228, 150));
            }
            var bar = new Rect(block.center.x - 31 * scale, block.y + 21 * scale, 62 * scale, 5 * scale);
            var background = new Color32(11, 19, 25, 230);
            SkillLabHud.Fill(new Rect(bar.x - 1, bar.y - 1, bar.width + 2, bar.height + 2), background);
            var full = actor.Kind == "caster" ? new Color32(77, 207, 182, 255) : new Color32(239, 126, 90, 255);
            SkillLabHud.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(actor.Hp.Value / actor.MaxHp.Value), bar.height), full);
            GUI.Label(new Rect(block.x, block.y, block.width, 21 * scale), name, _actorLabel);
            if (buffs != null)
                GUI.Label(new Rect(block.x, block.y + 28 * scale, block.width, 21 * scale), buffs, _buffLabel);
        }

        bool TryPlaceActorLabel(Rect preferred, float gap, out Rect placed)
        {
            // Try the anchor first, then alternating rows above and below it in stable snapshot order.
            for (int attempt = 0; attempt <= 8; attempt++)
            {
                int row = (attempt + 1) / 2 * (attempt % 2 == 0 ? 1 : -1);
                var block = preferred;
                block.y += row * (preferred.height + gap);
                if (block.xMin < 0 || block.xMax > Screen.width || block.yMin < 0 || block.yMax > Screen.height ||
                    SkillLabHud.OverlapsPanel(block)) continue;
                var padded = new Rect(block.x - gap, block.y - gap, block.width + 2 * gap, block.height + 2 * gap);
                bool overlaps = false;
                foreach (var occupied in _placedActorLabels.Values)
                {
                    if (!padded.Overlaps(occupied)) continue;
                    overlaps = true;
                    break;
                }
                if (overlaps) continue;
                placed = block;
                return true;
            }
            placed = default;
            return false;
        }

        void Clear()
        {
            foreach (var visual in _objects.Values)
            {
                if (visual == null) continue;
                visual.SetActive(false);
                Destroy(visual);
            }
            _objects.Clear();
            _bodies.Clear();
            _actorDisplays.Clear();
            _damageNumbers.Clear();
        }

        void OnDestroy()
        {
            Clear();
            _stage?.Clear();
            _feedback?.Dispose();
            foreach (var material in _materials)
                if (material != null) Destroy(material);
        }
    }
}
