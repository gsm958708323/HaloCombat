using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class CombatDebugHUD : MonoBehaviour
    {
        DemoCombatSession _s;

        public void Bind(DemoCombatSession s) => _s = s;

        void OnGUI()
        {
            if (_s == null) return;
            GUI.Box(new Rect(10, 10, 420, 260), "Combat Demo HUD");

            if (!_s.World.TryGetActor(_s.PlayerId, out var p)) return;
            var fsm = p.GetComp<StateMachineComp>();
            var dir = p.GetComp<SkillDirectorComp>();
            var tags = p.GetComp<TagComp>();
            var buf = p.GetComp<InputBufferComp>();
            var attr = p.GetComp<AttrComp>();
            var hp = p.GetComp<HealthComp>();
            var buff = p.GetComp<BuffComp>();
            var tf = p.GetComp<TransformComp>();

            float y = 40f;
            GUI.Label(new Rect(20, y, 400, 20), $"Frame={_s.Time.Frame}  State={fsm.Current}  Skill={dir.CurrentSkill}"); y += 18;
            GUI.Label(new Rect(20, y, 400, 20), $"Pos=({tf.Position.X:F2},{tf.Position.Y:F2},{tf.Position.Z:F2}) Buf={buf.HasBuffered}"); y += 18;
            GUI.Label(new Rect(20, y, 400, 20), $"Cancel={tags.Has(CommonTags.Cancel)} Grounded={tags.Has(CommonTags.Grounded)} Air={tags.Has(CommonTags.Airborne)}"); y += 18;
            GUI.Label(new Rect(20, y, 400, 20), $"ATK base/total={attr.BaseAtk:F1}/{attr.TotalAtk:F1}  HP={hp.Hp:F1}/{hp.MaxHp:F1}"); y += 18;
            GUI.Label(new Rect(20, y, 400, 20), $"Buffs={buff.AllBuffs.Count}"); y += 22;

            DrawDummy("MeleeDummy", _s.DummyMeleeId, ref y);
            DrawDummy("RangedDummy", _s.DummyRangedId, ref y);

            y += 8;
            GUI.Label(new Rect(20, y, 400, 60),
                "J/LMB Attack | K/Space Jump | WASD Move | H Debug Hit\nG1 cancel window -> J again = G2 (AoE+Firepool)");
        }

        void DrawDummy(string name, EntityId id, ref float y)
        {
            if (!_s.World.TryGetActor(id, out var a)) return;
            var hp = a.GetComp<HealthComp>();
            var fsm = a.GetComp<StateMachineComp>();
            GUI.Label(new Rect(20, y, 400, 20), $"{name}: HP={hp.Hp:F1} State={fsm.Current} Dead={hp.IsDead}");
            y += 18;
        }
    }
}
