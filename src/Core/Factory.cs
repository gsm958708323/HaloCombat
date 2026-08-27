using System;

namespace Combat.Core
{
    public sealed class FighterActorFactory : IActorFactory
    {
        readonly ComboTableSO _combos;
        readonly TimelineLibrary _timelines;

        public FighterActorFactory(ComboTableSO combos, TimelineLibrary timelines)
        {
            _combos = combos ?? new ComboTableSO();
            _timelines = timelines ?? new TimelineLibrary();
        }

        public Actor Create(in ActorSpawnSpec spec)
        {
            var actor = new Actor();
            actor.SetActive(true);
            string bp = spec.BlueprintId ?? "";

            if (bp == "projectile")
            {
                actor.AddComp(new TransformComp());
                actor.AddComp(new TagComp());
                actor.AddComp(new TeamComp(0));
                actor.AddComp(new ProjectileComp());
                return actor;
            }

            if (bp == "aoe")
            {
                actor.AddComp(new TransformComp());
                actor.AddComp(new TagComp());
                actor.AddComp(new TeamComp(0));
                actor.AddComp(new AoeComp());
                return actor;
            }

            bool stake = bp == "stake";
            var attr = new AttributeSet();
            actor.AddComp(new TransformComp());
            actor.AddComp(new TagComp());
            actor.AddComp(attr);
            actor.AddComp(new BuffComp());
            actor.AddComp(new TeamComp(stake ? 2 : 1));
            actor.AddComp(new HealthComp());
            actor.AddComp(new InputBufferComp());
            actor.AddComp(new StateMachineComp());
            actor.AddComp(new LocomotionComp());
            if (!stake)
            {
                actor.AddComp(new HitboxComp());
                actor.AddComp(new ComboComp(_combos));
                actor.AddComp(new PlayerCombatDriverComp());
                actor.AddComp(new SkillDirectorComp(_timelines));
                var loadout = new LoadoutComp();
                actor.AddComp(loadout);
                loadout.EquipNormalG1G2Defaults();
            }

            attr.InitFighterDefaults();
            return actor;
        }

        public void Release(Actor actor) => actor?.ResetForPool();
    }

    public static class DemoTables
    {
        public static TimelineLibrary MakeLib()
        {
            var lib = new TimelineLibrary();
            lib.Register(TimelineSO.G1());
            lib.Register(TimelineSO.G2());
            return lib;
        }

        public static ComboTableSO G1G2()
        {
            return new ComboTableSO
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
                    }
                }
            };
        }
    }
}
