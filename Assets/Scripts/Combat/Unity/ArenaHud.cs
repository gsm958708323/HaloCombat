using System.Text;
using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class ArenaHud : MonoBehaviour
    {
        ArenaBootstrap _bootstrap;
        bool _debug;
        static Rect Panel => new Rect(12, 12, Mathf.Min(Screen.width - 24, 600), 295);
        public static bool PointerOver(bool defeated) => CombatControls.OverPanel(Panel) ||
            defeated && CombatControls.OverPanel(new Rect(Screen.width/2f-160,Screen.height/2f-60,320,120));

        void SkillRow(int start, int end)
        {
            var text = new StringBuilder();
            for (int i = start; i < end && i < _bootstrap.SkillNames.Count; i++)
            {
                string name = _bootstrap.SkillNames[i];
                if (string.IsNullOrEmpty(name)) continue;
                if (text.Length > 0) text.Append("    ");
                text.Append(CombatControls.At(i).Label).Append("  ").Append(name);
            }
            if (text.Length > 0) GUILayout.Label(text.ToString());
        }
        void Awake()=>_bootstrap=GetComponent<ArenaBootstrap>();
        void OnGUI()
        {
            if(_bootstrap==null)return;var session=_bootstrap.Session;
            GUILayout.BeginArea(Panel,GUI.skin.box);
            GUILayout.Label(_bootstrap.Training ? "Arena · 木桩" : "Arena · 正式");
            if(session==null){GUILayout.Label(_bootstrap.LastLoadError);if(GUILayout.Button("Retry JSON load"))_bootstrap.Reload(false);GUILayout.EndArea();return;}
            if(session.World.TryGetActor(session.PlayerId,out var player))
            {
                var attr=player.GetComp<AttributeSet>();var energy=player.GetComp<EnergyComp>();
                GUILayout.Label("HP "+attr.GetBase(AttrId.Hp).ToString("F0")+" / "+attr.GetFinal(AttrId.MaxHp).ToString("F0")+"    Energy "+energy.Current.ToString("F0")+" / "+energy.Capacity.ToString("F0"));
                var ammo=player.GetComp<AmmoComp>();GUILayout.Label("Ammo "+ammo.Current+" / "+ammo.Capacity);
                GUILayout.Label("State "+player.GetComp<StateMachineComp>().Current+"    Enemies "+session.EnemyCount+"    Frame "+session.World.Time.Frame);
            }
            SkillRow(0, CombatControls.MainSkillCount);
            SkillRow(CombatControls.MainSkillCount, CombatControls.Count);
            GUILayout.Label("WASD 移动 · 鼠标瞄准 · 小键盘 1–4 · Esc 暂停 · F5 重载");
            _debug = GUILayout.Toggle(_debug, "发布槽调试");
            if (_debug) GUILayout.Label("F6 / F7 施放已加载的发布槽");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(session.Paused?"Resume":"Pause"))_bootstrap.SetPaused(!session.Paused);
            if(GUILayout.Button("Restart"))_bootstrap.Restart();
            if (GUILayout.Button(_bootstrap.Training ? "正式模式" : "木桩模式")) _bootstrap.SetTraining(!_bootstrap.Training);
            GUILayout.EndHorizontal();
            GUILayout.Label(_bootstrap.Status);GUILayout.EndArea();
            if(session.PlayerDead)
            {
                GUILayout.BeginArea(new Rect(Screen.width/2f-160,Screen.height/2f-60,320,120),GUI.skin.box);GUILayout.Label("PLAYER DEFEATED");GUILayout.Label("Combat stopped. Corpse cleanup continues.");
                if(GUILayout.Button("Restart from JSON"))_bootstrap.Restart();GUILayout.EndArea();
            }
        }
    }
}
