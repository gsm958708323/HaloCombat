using System;
using Combat.Core;

namespace Combat.Presentation
{
    public enum CombatLessonKind { Motor, Melee, ProjectileAoe, AiSummon }

    public readonly struct CombatLessonStep
    {
        public readonly string Title;
        public readonly string Explanation;
        public readonly int Frame;
        public CombatLessonStep(int frame, string title, string explanation)
        { Frame = frame; Title = title; Explanation = explanation; }
    }

    // Shared by the Unity lesson director and the CLI regression. No Unity clock,
    // scene objects or test-only Runner APIs are involved in the choreography.
    public sealed class CombatLesson
    {
        public const float Delta = 0.02f;
        public readonly CombatWorld World;
        public readonly CombatLessonKind Kind;
        public readonly EntityId Player;
        public readonly EntityId Target;
        public readonly CombatLessonStep[] Steps;
        public int Frame { get; private set; }
        public int DurationFrames => 500;
        public bool Finished => Frame >= DurationFrames;
        public int StepIndex
        {
            get { int index = 0; for (int i = 1; i < Steps.Length; i++) if (Frame >= Steps[i].Frame) index = i; return index; }
        }

        public CombatLesson(CombatWorld world, CombatLessonKind kind)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Kind = kind;
            Steps = MakeSteps(kind);
            Player = Spawn("fighter", -2f, 0f);
            if (kind == CombatLessonKind.Motor) Target = EntityId.Invalid;
            else if (kind == CombatLessonKind.AiSummon)
            {
                Target = Spawn("melee_guard", 4.5f, 1f);
                Actor target = Get(Target);
                var board = target.GetComp<BehaviorTreeComp>().Board;
                board.Home = target.GetComp<TransformComp>().Position;
                board.LeashRange = 5f;
            }
            else Target = Spawn("stake", kind == CombatLessonKind.Melee ? -1.35f : 2.4f, 0f);
        }

        public CombatLesson(CombatWorld world, CombatLessonKind kind, EntityId player, EntityId target)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Kind = kind;
            Steps = MakeSteps(kind);
            Player = player;
            Target = target;
            NormalizeAttachedActors();
        }

        void NormalizeAttachedActors()
        {
            var player = Get(Player);
            if (player == null || !player.TryGetComp<TransformComp>(out var playerTf)) return;
            playerTf.Position = new SimVec3(-2f, 0f, 0f);
            playerTf.YawDegrees = 0f;
            if (!Target.IsValid || !World.TryGetActor(Target, out var target) || target == null ||
                !target.TryGetComp<TransformComp>(out var targetTf)) return;
            targetTf.Position = Kind == CombatLessonKind.Melee
                ? new SimVec3(-1.35f, 0f, 0f)
                : Kind == CombatLessonKind.ProjectileAoe
                    ? new SimVec3(2.4f, 0f, 0f)
                    : new SimVec3(4.5f, 0f, 1f);
            if (Kind == CombatLessonKind.AiSummon && target.TryGetComp<BehaviorTreeComp>(out var bt))
                bt.Board.Home = targetTf.Position;
        }

        EntityId Spawn(string blueprint, float x, float z)
        {
            var id = World.SpawnActor(new ActorSpawnSpec(blueprint));
            var actor = Get(id);
            actor.GetComp<TransformComp>().Position = new SimVec3(x, 0f, z);
            // Training health is scenario configuration, not a change to combat rules.
            actor.GetComp<AttributeSet>().SetBase(AttrId.MaxHp, 1000f);
            actor.GetComp<AttributeSet>().SetBase(AttrId.Hp, 1000f);
            return id;
        }

        public Actor Get(EntityId id) => World.TryGetActor(id, out var actor) ? actor : null;
        public void Tick(bool scripted = true)
        {
            if (scripted) ApplyCommands();
            World.Tick(Delta);
            Frame++;
        }

        void Input(InputToken token) => Get(Player)?.GetComp<InputBufferComp>().Push(token);
        void Move(float x, float z) => Get(Player)?.GetComp<LocomotionComp>().RequestMoveIntent(x, z);
        void Deliver(IEffect effect, Actor source, Actor target = null, SimVec3? point = null)
        {
            if (source == null) return;
            World.Deliver(new[] { effect }, source, target, source.GetComp<AttributeSet>().GetFinal(AttrId.Atk), point);
        }

        void ApplyCommands()
        {
            Actor player = Get(Player);
            if (player == null) return;
            Move(0f, 0f);
            if (Kind == CombatLessonKind.Motor)
            {
                if (Frame >= 30 && Frame < 65) Move(1f, 0f);
                if (Frame >= 100 && Frame < 130) Move(0f, 1f);
                if (Frame == 120 || Frame == 240) Input(InputToken.Jump);
                if (Frame >= 240 && Frame < 270) Move(-1f, 0f);
                if (Frame >= 350 && Frame < 380) Move(0f, -1f);
                return;
            }
            if (Kind == CombatLessonKind.Melee)
            {
                if (Frame == 35 || Frame == 165 || Frame == 310) Input(InputToken.Attack);
                if (Frame == 172) Input(InputToken.Attack);
                if (Frame == 160 || Frame == 305)
                    Get(Target).GetComp<LocomotionComp>().RequestTeleport(player.GetComp<TransformComp>().Position + new SimVec3(0.65f, 0f, 0f));
                if (Frame == 325)
                    Deliver(new DamageEffect { Coeff = 1f, CanCrit = false, HitstopFrames = 5 }, Get(Target), player);
                if (Frame == 395) Input(Season2Tokens.Dodge);
                return;
            }
            if (Kind == CombatLessonKind.ProjectileAoe)
            {
                if (Frame == 35 || Frame == 80) Deliver(new SpawnProjectileEffect(CombatIds.HomingBolt), player);
                if (Frame >= 38 && Frame < 65) Get(Target).GetComp<LocomotionComp>().RequestMoveIntent(0f, 1f);
                if (Frame == 155) Deliver(new SpawnAoeEffect(CombatIds.FireGround), player, null, Get(Target).GetComp<TransformComp>().Position);
                if (Frame == 270) Deliver(new SpawnAoeEffect(CombatIds.AuraField), player, null, new SimVec3(0f, 0f, -1.3f));
                if (Frame == 280) Get(Target).GetComp<LocomotionComp>().RequestTeleport(new SimVec3(0f, 0f, -1.3f));
                if (Frame == 380) Get(Target).GetComp<LocomotionComp>().RequestTeleport(new SimVec3(2.4f, 0f, -1.3f));
                return;
            }
            if (Frame == 30) Deliver(new SpawnSummonEffect(CombatIds.MeleeSummon), player);
            if (Frame >= 55 && Frame < 85) Move(-1f, 0f);
            if (Frame == 190) Deliver(new KnockdownEffect { Duration = 0.8f }, player, Get(Target));
            if (Frame == 300)
            {
                var actors = World.RegistryActive();
                for (int i = 0; i < actors.Count; i++)
                    if (actors[i].TryGetComp<SummonComp>(out var summon) && summon.OwnerId == Player)
                    {
                        Deliver(new SpawnProjectileEffect(CombatIds.HomingBolt), actors[i]);
                        Deliver(new SpawnAoeEffect(CombatIds.AuraField), actors[i], null, actors[i].GetComp<TransformComp>().Position);
                        break;
                    }
            }
            if (Frame == 320) player.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "lesson-owner-dead" });
        }

        public static CombatLessonStep[] MakeSteps(CombatLessonKind kind)
        {
            switch (kind)
            {
                case CombatLessonKind.Motor: return new[] {
                    new CombatLessonStep(0, "移动意图", "输入只提交 Intent；Locomotion 在逻辑帧积分位置。"),
                    new CombatLessonStep(100, "跳跃与重力", "Jump 不创建独立 Activity：Root + Airborne，落地后恢复 Grounded。"),
                    new CombatLessonStep(230, "空中转向", "空中移动仍受 Motor 的 AirSteer 策略约束。"),
                    new CombatLessonStep(350, "逻辑与表现", "50 Hz 逻辑驱动位置；模型与脚底环只读取状态，不反写模拟。") };
                case CombatLessonKind.Melee: return new[] {
                    new CombatLessonStep(0, "一次完整攻击", "Move → Hitbox → Cue / Payload。发光刀弧不是伤害来源。"),
                    new CombatLessonStep(150, "Cancel 接续 G2", "第二次 Attack 必须经过 Combo 条件与 Cancel 窗口。"),
                    new CombatLessonStep(300, "命中与 Hitstop", "Damage 结算后发布 EvDamage / EvHitstop；表现层监听反馈。"),
                    new CombatLessonStep(390, "Dodge 无敌窗口", "无敌 Tag 与位移来自技能时间轴，结束时由 Clip 负责清理。") };
                case CombatLessonKind.ProjectileAoe: return new[] {
                    new CombatLessonStep(0, "追踪投射物", "SpawnProjectileEffect 创建 Actor；锁定线连接真实 HomingTarget。"),
                    new CombatLessonStep(140, "地面脉冲与灼烧", "AoE Pulse 投递 Effect，Burn 独立调度周期伤害并限制叠层。"),
                    new CombatLessonStep(260, "进入 / 离开光环", "Occupancy 按来源添加减速；离开时移除该来源 Modifier。"),
                    new CombatLessonStep(420, "生命周期清理", "Owner 清理 Projectile / AoE；视图只响应 Cleanup 释放资源。") };
                default: return new[] {
                    new CombatLessonStep(0, "召唤与自主选敌", "召唤物没有玩家 Input / Combo，使用自己的 Blackboard 与 BT。"),
                    new CombatLessonStep(140, "AI 与倒地停机", "Perception → Target → BT → Timeline；倒地会中断技能。"),
                    new CombatLessonStep(270, "独立 Source / Owner", "召唤物自身是伤害 Source；Owner 仅用于归属与生命周期。"),
                    new CombatLessonStep(315, "级联清理", "主人死亡 → 召唤物 → Projectile / AoE 后代，敌人继续存在。") };
            }
        }
    }
}


