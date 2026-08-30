using System;
using Combat.Core;

namespace Combat.Demos
{
    // 敌人 AI 用例：验证警戒、追击、回到出生点、巡逻、重新接敌和倒地停机。
    public static class EnemyAiDemo
    {
        public static void Run()
        {
            // melee_guard 带有出生点和追击边界，fighter 作为可被发现的目标。
            var world = SeasonTwoDemoSupport.NewWorld();
            var enemy = SeasonTwoDemoSupport.Spawn(world, "melee_guard", 0f, 0f);
            var player = SeasonTwoDemoSupport.Spawn(world, "fighter", 2f, 0f);
            var board = enemy.GetComp<BehaviorTreeComp>().Board;
            var director = enemy.GetComp<SkillDirectorComp>();
            var trace = new DemoTrace("EnemyAi", CombatCategories.EnemyAi, world, dt => SeasonTwoDemoSupport.Step(world, dt));
            trace.Step("init", "初始化 melee_guard 策略与 Home", () => $"{DemoTrace.Snapshot(enemy)} player={DemoTrace.Snapshot(player)}");
            bool sawAttack = false;
            // 初始距离在攻击范围附近，行为树应完成选敌并播放 G1。
            trace.AdvanceUntil("engage", "Guard 接敌并播放 G1", () =>
            {
                if (director.CurrentSkill == SkillNodeId.G1) sawAttack = true;
                return sawAttack && player.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f;
            }, 0.02f, 100, () => $"sawAttack={sawAttack} playerHp={player.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")} {DemoTrace.Snapshot(enemy)}");
            trace.Check("engage-result", "Guard 播放 G1 且玩家HP下降", sawAttack && player.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f,
                "播放 G1 且玩家HP下降", $"sawAttack={sawAttack} playerHp={player.GetComp<AttributeSet>().GetBase(AttrId.Hp).ToString("F1")}",
                () => DemoTrace.Snapshot(enemy));
            trace.Check("home-record", "记录 Guard 的 Home 出生点", board.Home.X == 0f && board.Home.Z == 0f,
                "Home=(0,0)", $"Home=({board.Home.X},{board.Home.Z})", () => DemoTrace.Snapshot(enemy));
            // 把玩家拉到远处，验证 guard 不会无限追击而是回到 Home。
            player.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);
            float minDistanceToHome = float.MaxValue;
            trace.AdvanceFor("return-home", "目标离开后 Guard 回到 Home", 0.02f, 150, () =>
            {
                float rx = enemy.GetComp<TransformComp>().Position.X - board.Home.X;
                float rz = enemy.GetComp<TransformComp>().Position.Z - board.Home.Z;
                float distance = (float)Math.Sqrt(rx * rx + rz * rz);
                if (distance < minDistanceToHome)
                    minDistanceToHome = distance;
                return $"distanceToHome={distance.ToString("F2")} min={minDistanceToHome.ToString("F2")} {DemoTrace.Snapshot(enemy)}";
            });
            trace.Check("return-home-result", "目标离开后回到 Home 附近", minDistanceToHome <= 0.6f && enemy.GetComp<TransformComp>().Position.X < 3f,
                "回到 Home 附近且未越界", $"minDistance={minDistanceToHome.ToString("F2")} x={enemy.GetComp<TransformComp>().Position.X.ToString("F2")}",
                () => DemoTrace.Snapshot(enemy));

            // 没有目标时，guard 在出生点附近执行巡逻移动。
            float patrolX = enemy.GetComp<TransformComp>().Position.X;
            float patrolZ = enemy.GetComp<TransformComp>().Position.Z;
            trace.AdvanceFor("patrol", "Home 附近无目标时巡逻", 0.05f, 10,
                () => $"position={enemy.GetComp<TransformComp>().Position.X.ToString("F2")},{enemy.GetComp<TransformComp>().Position.Z.ToString("F2")}");
            float movedX = enemy.GetComp<TransformComp>().Position.X - patrolX;
            float movedZ = enemy.GetComp<TransformComp>().Position.Z - patrolZ;
            trace.Check("patrol-result", "Home 附近无目标时产生巡逻位移", Math.Sqrt(movedX * movedX + movedZ * movedZ) > 0.3f,
                "巡逻位移>0.3", $"位移={Math.Sqrt(movedX * movedX + movedZ * movedZ).ToString("F2")}", () => DemoTrace.Snapshot(enemy));

            // 玩家重新进入警戒范围后，AI 应重新获得目标并再次进入攻击流程。
            player.GetComp<TransformComp>().Position = new SimVec3(1.5f, 0f, 0f);
            bool reacquired = false;
            trace.AdvanceUntil("reengage", "目标重新出现后再次接敌", () =>
            {
                reacquired = board.Target == player.Id || director.CurrentSkill == SkillNodeId.G1;
                return reacquired;
            }, 0.05f, 50, () => $"reacquired={reacquired} boardTarget={board.Target} skill={director.CurrentSkill}");
            trace.Check("reengage-result", "目标重新出现后恢复接敌流程", reacquired, "重新获得目标或播放 G1", $"reacquired={reacquired}",
                () => DemoTrace.Snapshot(enemy));

            // 倒地是状态机的硬门槛：停止移动、技能和后续 AI 决策。
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.4f } }, player, enemy, 10f);
            var stoppedAt = enemy.GetComp<TransformComp>().Position.X;
            trace.AdvanceFor("knockdown-stop", "Knockdown 后停止移动和攻击", 0.02f, 5,
                () => DemoTrace.Snapshot(enemy));
            trace.Check("knockdown-stop-result", "倒地后停止移动和攻击", enemy.GetComp<StateMachineComp>().Current == ActivityId.Knockdown &&
                enemy.GetComp<TransformComp>().Position.X == stoppedAt && !director.IsPlaying,
                "Activity=Knockdown、位置不变、技能停止", $"Activity={enemy.GetComp<StateMachineComp>().Current} x={enemy.GetComp<TransformComp>().Position.X} playing={director.IsPlaying}",
                () => DemoTrace.Snapshot(enemy));
            trace.Complete("Guard 接敌、回家、巡逻、重接敌与倒地停机验证完成");
        }
    }
}
