using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Demos
{
    public sealed class FighterActorFactory : IActorFactory
    {
        readonly CombatTime _time;
        readonly ComboTableSO _combos;
        readonly TimelineLibrary _timelines;
        readonly EffectFactory _effects;
        public FighterActorFactory(
            CombatTime time,
            ComboTableSO combos,
            TimelineLibrary timelines,
            EffectFactory effects)
        {
            _time = time;
            _combos = combos;
            _timelines = timelines;
            _effects = effects;
        }
        public Actor Create(in ActorSpawnSpec spec)
        {
            var actor = new Actor();
            actor.SetActive(true);
            actor.AddComp(new TransformComp());
            actor.AddComp(new TagComp());
            actor.AddComp(new InputBufferComp(_time));
            actor.AddComp(new StateMachineComp());
            actor.AddComp(new LocomotionComp(_time));
            actor.AddComp(new SkillDirectorComp(_timelines, _effects));
            actor.AddComp(new ComboComp(_combos));
            actor.AddComp(new PlayerCombatDriverComp());
            return actor;
        }
        public void Release(Actor actor) => actor?.ResetForPool();
    }
    /// <summary>
    /// 覆盖：开招、Cancel 区间、轴位移、取消接 G2、轴结束回 Root、
    /// Root 走路、Jump 落地回 Root、Hit 清缓冲停轴并回 Root。
    /// </summary>
    public class StateMachineDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();

            var library = TimelineLibrary.Create();
            var combos = new ComboTableSO
            {
                Entries = new[]
                {
                    new ComboEntry
                    {
                        PreSkills = Array.Empty<SkillNodeId>(),
                        Input = InputToken.Attack,
                        RequiredTags = Array.Empty<int>(),
                        Priority = 0,
                        ToSkill = SkillNodeId.G1,
                        Timeline = TimelineId.TL_G1
                    },
                    new ComboEntry
                    {
                        PreSkills = new[] { SkillNodeId.G1 },
                        Input = InputToken.Attack,
                        RequiredTags = new[] { CommonTags.Cancel.Value },
                        Priority = 10,
                        ToSkill = SkillNodeId.G2,
                        Timeline = TimelineId.TL_G2
                    },
                }
            };
            var effects = new EffectFactory(intents);
            var factory = new FighterActorFactory(time, combos, library, effects);
            var world = new CombatWorld(factory, intents, events, time);
            world.AddServicePhase(() =>
            {
                intents.Drain<AnimSignalIntent>(i =>
                    print($"[F{time.Frame}] Anim {i.Signal}"));
                intents.Drain<SpawnProjectileIntent>(i =>
                    print($"[F{time.Frame}] Proj spec={i.SpecValue} owner={i.Owner}"));
            });
            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var actor);
            var fsm = actor.GetComp<StateMachineComp>();
            var input = actor.GetComp<InputBufferComp>();
            var tags = actor.GetComp<TagComp>();
            var director = actor.GetComp<SkillDirectorComp>();
            var tf = actor.GetComp<TransformComp>();
            var loco = actor.GetComp<LocomotionComp>();
            void Dump(string title)
            {
                print(
                    $"[{title}] F{time.Frame} state={fsm.Current} skill={director.CurrentSkill} " +
                    $"cancel={tags.Has(CommonTags.Cancel)} pos=({tf.Position.X:F2},{tf.Position.Y:F2},{tf.Position.Z:F2}) " +
                    $"buf={input.HasBuffered}");
            }
            // ---- A. Root 走路 ----
            loco.SetMoveIntent(1f, 0f);
            world.Tick(0.10f);
            Dump("Root walk");
            loco.SetMoveIntent(0f, 0f);
            // ---- B. 开 G1：Cancel 区间 + 位移 ----
            input.Push(InputToken.Attack);
            world.Tick(0.05f);
            Dump("Open G1");
            world.Tick(0.05f); // ~0.10：Cancel 应 true，位移开始
            Dump("G1 cancel+move");
            float xBeforeCancelG2 = tf.Position.X;
            // ---- C. 取消接 G2 ----
            input.Push(InputToken.Attack);
            world.Tick(0.05f);
            Dump("Cancel into G2");
            // ---- D. 打完 G2，应回 Root ----
            for (int i = 0; i < 12; i++)
                world.Tick(0.05f);
            Dump("After G2 finished -> expect Root");
            if (fsm.Current != ActorStateId.Root)
                throw new Exception("Expected Root after timeline finished");
            if (tags.Has(CommonTags.Cancel))
                throw new Exception("Cancel should be cleared when interval exits/stop");
            print($"Displacement since before G2: dx={tf.Position.X - xBeforeCancelG2:F2}");
            // ---- E. Jump 落地回 Root ----
            input.Push(InputToken.Jump);
            world.Tick(0.02f);
            Dump("Jump start");
            for (int i = 0; i < 40; i++)
                world.Tick(0.05f);
            Dump("After land -> expect Root");
            if (fsm.Current != ActorStateId.Root)
                throw new Exception("Expected Root after land");
            // ---- F. Attack 中受击：停轴、清缓冲、硬直结束回 Root ----
            input.Push(InputToken.Attack);
            world.Tick(0.05f);
            Dump("Attack again");
            input.Push(InputToken.Attack); // 预输入，受击应清掉
            fsm.TryEnter(ActorStateId.Hit, new StateEnterArgs(fsm.Current, "Hit"));
            Dump("Enter Hit");
            if (input.HasBuffered)
                throw new Exception("Buffer should clear on Hit");
            if (director.IsPlaying)
                throw new Exception("Director should stop on Hit");
            for (int i = 0; i < 10; i++)
                world.Tick(0.05f);
            Dump("Hit recover -> expect Root");
            if (fsm.Current != ActorStateId.Root)
                throw new Exception("Expected Root after hit recover");
            // ---- G. Attack 时走路意图不应推动（只吃轴）----
            input.Push(InputToken.Attack);
            world.Tick(0.02f);
            float x0 = tf.Position.X;
            loco.SetMoveIntent(1f, 0f);
            world.Tick(0.10f); // 仍在 G1 早期；有轴速度才会动
            loco.SetMoveIntent(0f, 0f);
            Dump("Attack ignores walk intent (move only if axis key active)");
            print("StateMachineDemo PASSED");
        }
    }
}
