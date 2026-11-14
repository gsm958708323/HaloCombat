using Combat.Core;
using System;

namespace Combat.Config
{
    internal sealed class SkillSimulationFactory : IActorFactory
    {
        readonly CombatContentSnapshot _content;
        readonly PreviewDocument _preview;
        public SkillSimulationFactory(CombatContentSnapshot content, PreviewDocument preview)
        { _content = content; _preview = preview; }

        public Actor Create(in ActorSpawnSpec spec)
        {
            if(spec.BlueprintId=="construct")return ConstructActor.Create();
            var actor = new Actor();
            actor.AddComp(new TransformComp()); actor.AddComp(new TagComp());
            actor.AddComp(new TeamComp(spec.BlueprintId == "caster" ? 1 : 2));
            if (spec.BlueprintId == "projectile") { actor.AddComp(new ProjectileComp()); return actor; }
            if (spec.BlueprintId == "aoe") { actor.AddComp(new AoeComp()); return actor; }
            bool summoned=_content.Blueprints.TryGet(spec.BlueprintId,out var blueprint);
            if(!summoned&&spec.BlueprintId!="caster"&&spec.BlueprintId!="target")throw new InvalidOperationException("Unknown actor blueprint: "+spec.BlueprintId);
            var attributes = new AttributeSet(); attributes.InitFighterDefaults(); actor.AddComp(attributes);
            actor.AddComp(new BuffComp()); actor.AddComp(new HealthComp()); actor.AddComp(new StateMachineComp());
            actor.AddComp(new LocomotionComp()); actor.AddComp(new HitboxComp());
            if(summoned)
            {
                attributes.SetBase(AttrId.MaxHp,blueprint.Hp);attributes.SetBase(AttrId.Hp,blueprint.Hp);attributes.SetBase(AttrId.Atk,blueprint.Attack);
                attributes.SetBase(AttrId.Def,blueprint.Defense);attributes.SetBase(AttrId.MoveSpeed,blueprint.MoveSpeed);
                actor.GetComp<LocomotionComp>().CollisionRadius=blueprint.CollisionRadius;
                actor.AddComp(new SummonComp());
                actor.AddComp(new BehaviorTreeComp(SummonBehavior.Create(blueprint.Skill,blueprint.HoldDuration),board=>
                {board.AcquireRadius=blueprint.AcquireRadius;board.AttackRange=blueprint.AttackRange;board.FollowRange=blueprint.FollowRange;}));
            }
            actor.AddComp(new SkillDirectorComp(_content.Timelines));
            if (spec.BlueprintId == "caster"||summoned)
            {
                actor.AddComp(summoned?new EnergyComp(blueprint.Energy,blueprint.Recovery,blueprint.InitialEnergy):new EnergyComp(_preview.Energy, _preview.Recovery, _preview.InitialEnergy));
                actor.AddComp(summoned?new AmmoComp(blueprint.Ammo,blueprint.InitialAmmo):new AmmoComp(_preview.Ammo,_preview.InitialAmmo));actor.AddComp(new TrackedProjectilesComp());
                actor.AddComp(new SkillCastComp(_content.Skills));
            }
            return actor;
        }

        public void Release(Actor actor) => actor.ResetForPool();
    }
}
