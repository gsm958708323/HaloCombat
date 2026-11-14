using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class RuntimeActorDefinition
    {
        public string Id,Role,View;
        public int Team;
        public float Hp,Attack,Defense,MoveSpeed,Shield,CritRate,DamageDealt,DamageTaken,CollisionRadius,Energy,InitialEnergy,Recovery;
        public SkillNodeId Skill,DodgeSkill;
        public SkillNodeId[] Slots=Array.Empty<SkillNodeId>();
        public BtNode Tree;
        public float AcquireRadius,AttackRange,FollowRange,LeashRange,PatrolRadius;
    }

    // Gameplay factories consume domain definitions; they never read authoring JSON or Unity assets.
    public sealed class RuntimeActorFactory : IActorFactory
    {
        readonly IReadOnlyDictionary<string,RuntimeActorDefinition> _actors;
        readonly BakedCombatData _data;
        readonly SkillCatalog _skills;
        public RuntimeActorFactory(BakedCombatData data,SkillCatalog skills,IReadOnlyDictionary<string,RuntimeActorDefinition> actors)
        {_data=data;_skills=skills;_actors=actors;}
        public Actor Create(in ActorSpawnSpec spec)
        {
            var actor=new Actor();actor.AddComp(new TransformComp());actor.AddComp(new TagComp());
            if(spec.BlueprintId=="projectile"||spec.BlueprintId=="aoe")
            {actor.AddComp(new TeamComp(0));if(spec.BlueprintId=="projectile")actor.AddComp(new ProjectileComp());else actor.AddComp(new AoeComp());return actor;}
            if(!_actors.TryGetValue(spec.BlueprintId??"",out var definition))throw new InvalidOperationException("Unknown runtime actor blueprint: "+spec.BlueprintId);
            actor.AddComp(new TeamComp(definition.Team));var attr=new AttributeSet();actor.AddComp(attr);
            attr.SetBase(AttrId.MaxHp,definition.Hp);attr.SetBase(AttrId.Hp,definition.Hp);attr.SetBase(AttrId.Atk,definition.Attack);
            attr.SetBase(AttrId.Def,definition.Defense);attr.SetBase(AttrId.MoveSpeed,definition.MoveSpeed);attr.SetBase(AttrId.Shield,definition.Shield);
            attr.SetBase(AttrId.CritRate,definition.CritRate);attr.SetBase(AttrId.DmgDealMul,definition.DamageDealt);attr.SetBase(AttrId.DmgTakenMul,definition.DamageTaken);
            actor.AddComp(new BuffComp());actor.AddComp(new HealthComp());actor.AddComp(new StateMachineComp());
            actor.AddComp(new LocomotionComp{CollisionRadius=definition.CollisionRadius});
            if(definition.Role=="target")return actor;
            actor.AddComp(new HitboxComp());
            actor.AddComp(new EnergyComp(definition.Energy,definition.Recovery,definition.InitialEnergy));actor.AddComp(new SkillCastComp(_skills));
            if(definition.Role=="player")
            {
                actor.AddComp(new InputBufferComp());actor.AddComp(new ComboComp(_data.Combo));
                actor.AddComp(new RuntimePlayerDriverComp(_data.Combo,definition.DodgeSkill));var slots=new LoadoutComp();actor.AddComp(slots);
                for(int i=0;i<definition.Slots.Length;i++)if(_skills.TryGet(definition.Slots[i],out var skill))slots.EquipSkill((SkillSlot)i,skill.Id,skill.Timeline);
            }
            else
            {
                actor.AddComp(new PerceptionComp(definition.AcquireRadius));
                actor.AddComp(new BehaviorTreeComp(definition.Tree,board=>
                {board.AcquireRadius=definition.AcquireRadius;board.AttackRange=definition.AttackRange;board.FollowRange=definition.FollowRange;board.LeashRange=definition.LeashRange;board.PatrolRadius=definition.PatrolRadius;}));
                if(definition.Role=="summon")actor.AddComp(new SummonComp());
            }
            // Inputs/perception/BT run before the director, preserving the original same-step
            // start semantics instead of adding a frame to every authored payload and clip.
            actor.AddComp(new SkillDirectorComp(_data.Timelines));
            return actor;
        }
        public void Release(Actor actor)=>actor?.ResetForPool();
    }

    public sealed class RuntimePlayerDriverComp : Comp
    {
        readonly ComboTableDefinition _combo;readonly SkillNodeId _dodge;
        public RuntimePlayerDriverComp(ComboTableDefinition combo,SkillNodeId dodge){_combo=combo;_dodge=dodge;}
        public override bool WantsTick=>true;
        public override void Tick(float dt)
        {
            var tags=Self.GetComp<TagComp>();var input=Self.GetComp<InputBufferComp>();
            if(tags.Has(CommonTags.Dead)||tags.Has(CommonTags.Stunned)||tags.Has(CommonTags.Downed)||!input.TryPeek(out var token))return;
            if(token==InputToken.Jump)
            {if(tags.Has(CommonTags.Grounded)){input.Consume();Self.GetComp<LocomotionComp>().ImpulseJump();}return;}
            SkillNodeId selected;
            if(token==Season2Tokens.Dodge)selected=_dodge;
            else
            {
                if(!_combo.TryResolve(Self.GetComp<SkillDirectorComp>().CurrentSkill,token,tags,out var result))return;
                selected=result.ToSkill;
            }
            var transform=Self.GetComp<TransformComp>();
            if(selected.IsValid&&Self.GetComp<SkillCastComp>().TryCast(selected,new SkillAim(EntityId.Invalid,transform.Position,transform.YawDegrees,false)))input.Consume();
        }
    }
}
