using System;

namespace Combat.Core
{
    public sealed class FighterActorFactory : IActorFactory
    {
        readonly BakedCombatData _data;
        readonly ComboTableSO _combos;
        readonly TimelineLibrary _timelines;

        public FighterActorFactory(ComboTableSO combos, TimelineLibrary timelines)
        {
            _combos = combos ?? new ComboTableSO();
            _timelines = timelines ?? new TimelineLibrary();
            _data = new BakedCombatData
            {
                Combo = _combos,
                Timelines = _timelines,
                Projectiles = new ProjectileCatalog(),
                Aoes = new AoeCatalog(),
                Summons = new SummonCatalog(),
                Cues = CueLibrary.DefaultCombat(),
                Motor = MotorConfig.SeasonOneDefaults()
            };
        }

        public FighterActorFactory(BakedCombatData data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _combos = data.Combo ?? new ComboTableSO();
            _timelines = data.Timelines ?? new TimelineLibrary();
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
            bool enemy = bp == "melee_ai" || bp == "melee_ai_narrow" || bp == "melee_guard" || bp == "ranged_ai";
            bool summon = bp == "summon";
            bool fighter = bp == "fighter" || (!stake && !enemy && !summon);

            if (summon)
                return CreateCombatant(actor, bp, false, true);
            if (enemy)
                return CreateCombatant(actor, bp, false, false);
            if (fighter)
                return CreateCombatant(actor, bp, true, false);

            // Unknown blueprints retain the original stake-like target dummy shape.
            return CreateCombatant(actor, bp, false, false, true);
        }

        Actor CreateCombatant(Actor actor, string bp, bool player, bool isSummon, bool stake = false)
        {
            var attr = new AttributeSet();
            actor.AddComp(new TransformComp());
            actor.AddComp(new TagComp());
            actor.AddComp(attr);
            actor.AddComp(new BuffComp());
            actor.AddComp(new TeamComp(player || isSummon ? 1 : 2));
            actor.AddComp(new HealthComp());
            actor.AddComp(new StateMachineComp());
            actor.AddComp(new LocomotionComp());
            if (player)
            {
                actor.AddComp(new InputBufferComp());
                actor.AddComp(new HitboxComp());
                actor.AddComp(new ComboComp(_combos));
                actor.AddComp(new PlayerCombatDriverComp());
                actor.AddComp(new SkillDirectorComp(_timelines));
                var loadout = new LoadoutComp();
                actor.AddComp(loadout);
                loadout.EquipNormalG1G2Defaults();
            }
            else if (!stake)
            {
                actor.AddComp(new HitboxComp());
                BtNode tree;
                if (bp == "melee_guard")
                    tree = BtFactory.MeleeGuard(SkillNodeId.G1, TimelineId.TL_G1);
                else if (bp == "ranged_ai")
                    tree = BtFactory.Ranged(SkillNodeId.Ranged, TimelineId.TL_Homing);
                else
                    tree = isSummon
                        ? BtFactory.SummonMelee(SkillNodeId.G1, TimelineId.TL_G1)
                        : BtFactory.MeleePuncher(SkillNodeId.G1, TimelineId.TL_G1);

                float acquire = bp == "melee_ai_narrow" ? 2.5f : 8f;
                if (isSummon) acquire = 8f;
                // Perception is intentionally before BT; both are independent orchestration
                // components and neither is allowed to perform settlement.
                actor.AddComp(new PerceptionComp(acquire));
                actor.AddComp(new BehaviorTreeComp(tree, board =>
                {
                    board.AcquireRadius = acquire;
                    board.AttackRange = bp == "ranged_ai" ? 4f : 1.15f;
                    board.FollowRange = isSummon ? 2f : 1.5f;
                    board.LeashRange = bp == "melee_guard" ? 5f : 20f;
                    board.PatrolRadius = bp == "melee_guard" ? 1.5f : 0f;
                }));
                actor.AddComp(new SkillDirectorComp(_timelines));
                if (isSummon)
                    actor.AddComp(new SummonComp());
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
            lib.Register(TimelineSO.Dodge());
            lib.Register(TimelineSO.Homing());
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

        public static void ResetG1MeleeDefaults()
        {
            TimelineSO.G1Melee.Damage.Coeff = 1f;
            TimelineSO.G1Melee.Damage.CanCrit = true;
            TimelineSO.G1Melee.Damage.UseSnapshotAtk = true;
            TimelineSO.G1Melee.Damage.HitstopFrames = 3;
            TimelineSO.G1Melee.Stun = new HitStunEffect { Duration = 0.35f };
            TimelineSO.G1Melee.Knockback = new KnockbackEffect { Distance = 0.4f };
            TimelineSO.G1Melee.IFrame = null;
            TimelineSO.G1Melee.ClearCache();
        }
    }
}
