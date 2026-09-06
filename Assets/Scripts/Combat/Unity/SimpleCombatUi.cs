using Combat.Game;
using UnityEngine;

namespace Combat.Unity.Game
{
    public sealed class SimpleCombatUi : MonoBehaviour
    {
        public AppBootstrap App;

        void OnGUI()
        {
            var flow = App != null ? App.Flow : null;
            if (flow == null)
                return;
            const float width = 260f;
            const float height = 54f;
            var area = new Rect(24f, 24f, width, 220f);
            GUILayout.BeginArea(area, GUI.skin.box);
            if (flow.State == FlowState.Title)
            {
                GUILayout.Label("HALO COMBAT");
                GUILayout.Label("WASD 移动  J 攻击  Space 跳跃  Shift 闪避");
                if (GUILayout.Button("开始战斗", GUILayout.Height(height)))
                    flow.StartRun();
            }
            else if (flow.State == FlowState.Arena && flow.Session != null)
            {
                GUILayout.Label("Arena  Frame " + flow.Session.Frame);
                GUILayout.Label(flow.Session.Paused ? "已暂停" : "战斗中");
                if (flow.Session.Paused && GUILayout.Button("继续", GUILayout.Height(height)))
                    flow.Session.TogglePause();
                if (GUILayout.Button("返回标题", GUILayout.Height(height)))
                    flow.GoTitle();
            }
            else if (flow.State == FlowState.Result)
            {
                GUILayout.Label(
                    flow.Session != null && flow.Session.PlayerWin ? "VICTORY" : "DEFEAT"
                );
                if (flow.ResultReady)
                {
                    if (GUILayout.Button("再战", GUILayout.Height(height)))
                        flow.ConfirmResultRetry();
                    if (GUILayout.Button("返回标题", GUILayout.Height(height)))
                        flow.ConfirmResultTitle();
                }
            }
            GUILayout.EndArea();
        }
    }
}
