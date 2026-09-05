using Combat.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Combat.TrainingCamp
{
    /// <summary>Compact operator HUD: every control routes through TrainingCampController.</summary>
    public sealed class TrainingCampPanel : MonoBehaviour
    {
        TrainingCampRunner _runner;
        TrainingCampController _controller;
        GUIStyle _title, _label, _button, _box;
        Canvas _canvasMarker;

        void Awake()
        {
            _runner = GetComponent<TrainingCampRunner>();
            _controller = GetComponent<TrainingCampController>();
            var canvasObject = new GameObject("TrainingCampCanvas");
            canvasObject.transform.SetParent(transform);
            _canvasMarker = canvasObject.AddComponent<Canvas>();
            _canvasMarker.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }
        void InitStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(.35f, .9f, 1f) } };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(.84f, .9f, .96f) } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 27 };
            _box = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(12, 12, 10, 10) };
        }
        void OnGUI()
        {
            if (_runner == null || !_runner.PanelVisible || _runner.World == null) return;
            InitStyles(); GUI.color = Color.white;
            GUILayout.BeginArea(new Rect(16, 16, 430, Screen.height - 32), _box);
            GUILayout.Label("ARPG COMBAT / VALIDATION SANDBOX", _title);
            GUILayout.Label("SANDBOX  •  manual operator mode  •  Tab hide", _label);
            var dummy = _runner.World.TryGetActor(_runner.DummyId, out var d) ? d : null;
            var player = _runner.World.TryGetActor(_runner.PlayerId, out var p) ? p : null;
            GUILayout.Label("Dummy: " + (_runner.DummyAiEnabled ? "AI ACTIVE" : "PASSIVE") + "   |   HP: " + (dummy != null ? dummy.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F0") : "-") + " ∞", _label);
            GUILayout.Label("Player: " + (player != null ? player.GetComp<StateMachineComp>().Current.ToString() : "-") + "   Tags: " + Tags(player), _label);
            GUILayout.Label("Actors " + _runner.World.RegistryActive().Count + "   Projectile " + TrainingCampProbe.Count<ProjectileComp>(_runner.World) + "   AOE " + TrainingCampProbe.Count<AoeComp>(_runner.World) + "   Summon " + TrainingCampProbe.Count<SummonComp>(_runner.World), _label);
            GUILayout.Space(5);
            GUILayout.Label("CORE ACTIONS", _label); Row("Reset World", _controller.ResetWorld, "Toggle Dummy AI", _controller.ToggleDummyAI); Row("Attack", _controller.Attack, "Dodge", _controller.Dodge); Row("Jump", _controller.Jump, "Kill / Respawn", _controller.KillRespawnPlayer); Row("Knockdown Dummy", _controller.KnockdownDummy, "Clear Runtime", _controller.ClearRuntimeObjects);
            GUILayout.Label("EFFECT / RUNTIME", _label); Row("Fireball", _controller.SpawnFireball, "Homing Bolt", _controller.SpawnHomingProjectile); Row("Fire Ground", _controller.SpawnFireGround, "Aura", _controller.SpawnAura); Row("Summon", _controller.Summon, "Apply Burn", _controller.ApplyBuff); Row("Dispel Burn", _controller.DispelBuff, "Current Check", _controller.RunCurrentCheck); Row("Run ALL pure C# checks", _controller.RunAllChecks, null, null);
            GUILayout.Space(5); GUILayout.Label("LAST: " + _runner.LastOperation, _label);
            GUILayout.Label("EVENT STREAM", _label); foreach (var entry in _runner.EventStream) GUILayout.Label(entry, _label);
            GUILayout.EndArea();
        }
        void Row(string a, System.Action ca, string b, System.Action cb)
        {
            GUILayout.BeginHorizontal(); if (!string.IsNullOrEmpty(a) && GUILayout.Button(a, _button)) ca(); if (!string.IsNullOrEmpty(b) && GUILayout.Button(b, _button)) cb(); GUILayout.EndHorizontal();
        }
        static string Tags(Actor a)
        {
            if (a == null) return "-"; var t = a.GetComp<TagComp>(); string s = "";
            if (t.Has(CommonTags.Grounded)) s += "Grounded "; if (t.Has(CommonTags.Airborne)) s += "Airborne "; if (t.Has(CommonTags.Cancel)) s += "Cancel "; if (t.Has(CommonTags.Invincible)) s += "IFrame "; if (t.Has(CommonTags.Downed)) s += "Downed "; return string.IsNullOrEmpty(s) ? "-" : s;
        }
    }
}
