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
            var tagComp = new TagComp();
            actor.AddComp(tagComp);
            var inputComp = new InputBufferComp(_time);
            actor.AddComp(inputComp);
            actor.AddComp(new StateMachineComp(_combos, tagComp, inputComp, _time));
            actor.AddComp(new SkillDirectorComp(_timelines, _effects));
            actor.AddComp(new ComboComp(_combos));
            actor.AddComp(new PlayerCombatDriverComp());
            return actor;
        }
        public void Release(Actor actor) => actor?.ResetForPool();
    }

    public class TimelineEffectDemo : MonoBehaviour
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
                    // 开招：无当前技能节点 → G1
                    new ComboEntry
                    {
                        PreSkills = Array.Empty<SkillNodeId>(),
                        Input = InputToken.Attack,
                        RequiredTags = Array.Empty<int>(),
                        Priority = 0,
                        ToSkill = SkillNodeId.G1,
                        Timeline = TimelineId.TL_G1
                    },
                    // G1 取消窗接 G2
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

            // Service 阶段仅打印跨实体 Intent（投射物真正生成留给下一步）
            world.AddServicePhase(() =>
            {
                intents.Drain<AnimSignalIntent>(i =>
                    print($"[F{time.Frame}] AnimSignal {i.Signal} from {i.Source}"));
                intents.Drain<SpawnProjectileIntent>(i =>
                    print($"[F{time.Frame}] SpawnProjectile spec={i.SpecValue} owner={i.Owner}"));
            });

            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var actor);
            var input = actor.GetComp<InputBufferComp>();
            var tags = actor.GetComp<TagComp>();
            var director = actor.GetComp<SkillDirectorComp>();
            var fsm = actor.GetComp<StateMachineComp>();

            // 开招
            input.Push(InputToken.Attack);
            world.Tick(0.05f); // Driver 匹配 G1 并 Play
            print($"After open: state={fsm.Current}, skill={director.CurrentSkill}, cancel={tags.Has(CommonTags.Cancel)}");

            // 推到取消窗内
            world.Tick(0.05f); // 累计 ~0.10，Cancel 应已加上（键在 0.05）
            print($"At ~0.10: cancel={tags.Has(CommonTags.Cancel)}");

            // 取消接 G2
            input.Push(InputToken.Attack);
            world.Tick(0.05f);
            print($"After cancel G2: skill={director.CurrentSkill}");

            // 继续推，直到 G1 旧轴已换；再推到生成物键（在 G1 上）。本 demo 换轴后走 G2，不再生成 901。
            // 再开一轮专测投射物：
            // 重置体验：直接 Play G1 并推进到 0.25
            director.Play(SkillNodeId.G1, TimelineId.TL_G1);
            for (int i = 0; i < 6; i++)
                world.Tick(0.05f); // 0.30s

            print($"Done. skill={director.CurrentSkill}, cancel={tags.Has(CommonTags.Cancel)}");

            // 受击停轴
            input.Push(InputToken.Attack);
            fsm.TryEnter(ActorStateId.Hit, new StateEnterArgs(ActorStateId.Attack, "Hit"));
            print($"On Hit: skill={director.CurrentSkill}, buffered={input.HasBuffered}, state={fsm.Current}");
        }
    }
}
