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
            bool sawAttack = false;
            // 初始距离在攻击范围附近，行为树应完成选敌并播放 G1。
            for (int i = 0; i < 100; i++)
            {
                SeasonTwoDemoSupport.Step(world, 0.02f);
                if (director.CurrentSkill == SkillNodeId.G1)
                    sawAttack = true;
            }
            SeasonTwoDemoSupport.Assert(sawAttack && player.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 100f, "enemy attacks");
            SeasonTwoDemoSupport.Assert(board.Home.X == 0f && board.Home.Z == 0f, "enemy records home");
            // 把玩家拉到远处，验证 guard 不会无限追击而是回到 Home。
            player.GetComp<TransformComp>().Position = new SimVec3(20f, 0f, 0f);
            float minDistanceToHome = float.MaxValue;
            for (int i = 0; i < 150; i++)
            {
                SeasonTwoDemoSupport.Step(world, 0.02f);
                float rx = enemy.GetComp<TransformComp>().Position.X - board.Home.X;
                float rz = enemy.GetComp<TransformComp>().Position.Z - board.Home.Z;
                float distance = (float)Math.Sqrt(rx * rx + rz * rz);
                if (distance < minDistanceToHome)
                    minDistanceToHome = distance;
            }
            SeasonTwoDemoSupport.Assert(minDistanceToHome <= 0.6f && enemy.GetComp<TransformComp>().Position.X < 3f, "enemy leash");

            // 没有目标时，guard 在出生点附近执行巡逻移动。
            float patrolX = enemy.GetComp<TransformComp>().Position.X;
            float patrolZ = enemy.GetComp<TransformComp>().Position.Z;
            for (int i = 0; i < 10; i++) SeasonTwoDemoSupport.Step(world, 0.05f);
            float movedX = enemy.GetComp<TransformComp>().Position.X - patrolX;
            float movedZ = enemy.GetComp<TransformComp>().Position.Z - patrolZ;
            SeasonTwoDemoSupport.Assert(Math.Sqrt(movedX * movedX + movedZ * movedZ) > 0.3f, "enemy patrols at home");

            // 玩家重新进入警戒范围后，AI 应重新获得目标并再次进入攻击流程。
            player.GetComp<TransformComp>().Position = new SimVec3(1.5f, 0f, 0f);
            bool reacquired = false;
            for (int i = 0; i < 50; i++)
            {
                SeasonTwoDemoSupport.Step(world, 0.05f);
                if (board.Target == player.Id || director.CurrentSkill == SkillNodeId.G1)
                {
                    reacquired = true;
                    break;
                }
            }
            SeasonTwoDemoSupport.Assert(reacquired, "enemy reengages");

            // 倒地是状态机的硬门槛：停止移动、技能和后续 AI 决策。
            world.Deliver(new IEffect[] { new KnockdownEffect { Duration = 0.4f } }, player, enemy, 10f);
            var stoppedAt = enemy.GetComp<TransformComp>().Position.X;
            for (int i = 0; i < 5; i++) SeasonTwoDemoSupport.Step(world, 0.02f);
            SeasonTwoDemoSupport.Assert(enemy.GetComp<StateMachineComp>().Current == ActivityId.Knockdown &&
                enemy.GetComp<TransformComp>().Position.X == stoppedAt && !director.IsPlaying, "downed stops enemy");
            CombatLog.Info(CombatCategories.EnemyAi, "EnemyAiDemo PASSED");
        }
    }
}
