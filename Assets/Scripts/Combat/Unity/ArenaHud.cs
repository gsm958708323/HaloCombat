using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class ArenaHud : MonoBehaviour
    {
        ArenaBootstrap _bootstrap;
        void Awake()=>_bootstrap=GetComponent<ArenaBootstrap>();
        void OnGUI()
        {
            if(_bootstrap==null)return;var session=_bootstrap.Session;
            GUILayout.BeginArea(new Rect(12,12,400,270),GUI.skin.box);
            GUILayout.Label("HALOCOMBAT / JSON ARENA");
            if(session==null){GUILayout.Label(_bootstrap.LastLoadError);if(GUILayout.Button("Retry JSON load"))_bootstrap.Reload(false);GUILayout.EndArea();return;}
            if(session.World.TryGetActor(session.PlayerId,out var player))
            {
                var attr=player.GetComp<AttributeSet>();var energy=player.GetComp<EnergyComp>();
                GUILayout.Label("HP "+attr.GetBase(AttrId.Hp).ToString("F0")+" / "+attr.GetFinal(AttrId.MaxHp).ToString("F0")+"    Energy "+energy.Current.ToString("F0")+" / "+energy.Capacity.ToString("F0"));
                GUILayout.Label("State "+player.GetComp<StateMachineComp>().Current+"    Enemies "+session.EnemyCount+"    Frame "+session.World.Time.Frame);
            }
            GUILayout.Label("WASD / Arrows: move | Mouse: aim | Hold LMB / 1: primary");
            GUILayout.Label("2 / RMB, 3 / MMB, 5: skills | Space: roll | Q / E: skills");
            GUILayout.Label("Esc: pause | F5: reload project + published slots | F6 / F7: cast");
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(session.Paused?"Resume":"Pause"))_bootstrap.SetPaused(!session.Paused);
            if(GUILayout.Button("Restart"))_bootstrap.Restart();
            if(GUILayout.Button(_bootstrap.Training?"Combat mode":"Training target"))_bootstrap.SetTraining(!_bootstrap.Training);
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
