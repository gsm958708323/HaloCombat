using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    public sealed class RuntimeContentSnapshot
    {
        public BakedCombatData Data {get;internal set;}
        public IReadOnlyDictionary<string,RuntimeActorDefinition> Actors {get;internal set;}
        public IActorFactory CreateFactory(SkillCatalog skills)=>new RuntimeActorFactory(Data,skills,Actors);
    }
    internal static class RuntimeContentCompiler
    {
        public static RuntimeContentSnapshot Compile(SkillProjectDocument project,CombatContentSnapshot content)
        {
            var source=project.Runtime;if(source==null)return null;
            string path="$.runtime";
            if(source.Motor==null||source.Actors==null||source.Combos==null||source.Cues==null)Fail(path,"Runtime arrays and motor are required.");
            if(source.Actors.Length>256||source.Combos.Length>1024||source.Cues.Length>512)Fail(path,"Runtime collection size is invalid.");
            var m=source.Motor;N(m.Gravity,-1000,-.01f,path+".motor.gravity");N(m.JumpSpeed,.01f,100,path+".motor.jumpSpeed");
            N(m.AirSteer,0,1,path+".motor.airSteer");N(m.GroundY,-1000,1000,path+".motor.groundY");N(m.StickDeadzone,0,1,path+".motor.stickDeadzone");
            var keys=new HashSet<string>();var combo=new List<ComboEntry>();
            foreach(var row in source.Combos)
            {
                if(row==null)Fail(path+".combos","Null row.");string p=path+".combos["+row.Id+"]";Identity(row.Id,keys,p);
                if(string.IsNullOrWhiteSpace(row.Input)||row.RequiredTags==null||row.RequiredTags.Any(t=>t<=0)||row.PreSkills==null)Fail(p,"Invalid input, tags or predecessor array.");
                var to=Skill(content,row.ToSkill,p+".toSkill");
                combo.Add(new ComboEntry{Input=new InputToken(row.Input),Priority=row.Priority,RequiredTags=(int[])row.RequiredTags.Clone(),
                    PreSkills=row.PreSkills.Select(id=>Skill(content,id,p+".preSkills").Id).ToArray(),ToSkill=to.Id,Timeline=to.Timeline});
            }
            var cues=new CueLibrary();var cueIds=new HashSet<int>();
            foreach(var cue in source.Cues)
            {
                if(cue==null)Fail(path+".cues","Null cue.");string p=path+".cues["+cue.Id+"]";
                if(cue.Id<=0||!cueIds.Add(cue.Id)||string.IsNullOrWhiteSpace(cue.Key))Fail(p,"Missing/duplicate cue identity or key.");
                if(cue.Kind!="cube"&&cue.Kind!="prefab")Fail(p+".kind","Expected cube or prefab.");
                if(cue.Kind=="prefab"&&string.IsNullOrWhiteSpace(cue.Resource))Fail(p+".resource","Prefab resource key is required.");
                N(cue.Lifetime,.001f,120,p+".lifetime");
                cues.Register(new CueDef{CueId=cue.Id,PrefabKey=cue.Key,SfxKey=cue.Sound??"",LifeTime=cue.Lifetime,PresentationKind=cue.Kind,ResourceKey=cue.Resource});
            }
            foreach(int cue in content.RequiredCues)if(!cueIds.Contains(cue))Fail("$.runtime.cues["+cue+"]","Referenced Cue is missing.");
            var data=new BakedCombatData{Combo=new ComboTableDefinition{Entries=combo.ToArray()},Timelines=content.Timelines,Projectiles=content.Projectiles,Aoes=content.Aoes,
                Summons=content.Summons,Cues=cues,Motor=new MotorConfig{Gravity=m.Gravity,JumpSpeed=m.JumpSpeed,AirSteer=m.AirSteer,GroundY=m.GroundY,StickDeadzone=m.StickDeadzone}};
            var actors=new Dictionary<string,RuntimeActorDefinition>();
            foreach(var actor in source.Actors)
            {
                if(actor==null)Fail(path+".actors","Null actor.");string p=path+".actors["+actor.Id+"]";
                if(string.IsNullOrWhiteSpace(actor.Id)||actor.Id=="projectile"||actor.Id=="aoe"||actors.ContainsKey(actor.Id))Fail(p+".id","Missing, reserved or duplicate actor identity.");
                if(Array.IndexOf(new[]{"player","target","ai","summon"},actor.Role)<0)Fail(p+".role","Unknown actor role.");
                N(actor.Hp,.001f,1000000000,p+".hp");N(actor.Attack,0,10000000,p+".attack");N(actor.Defense,0,10000000,p+".defense");N(actor.MoveSpeed,0,1000,p+".moveSpeed");
                N(actor.Shield,0,1000000000,p+".shield");N(actor.CritRate,0,1,p+".critRate");N(actor.DamageDealt,0,1000,p+".damageDealt");N(actor.DamageTaken,0,1000,p+".damageTaken");N(actor.CollisionRadius,0,100,p+".collisionRadius");
                N(actor.Energy,0,1000000,p+".energy");N(actor.Recovery,0,1000000,p+".recovery");
                N(actor.AcquireRadius,.001f,1000,p+".acquireRadius");N(actor.AttackRange,.001f,1000,p+".attackRange");N(actor.FollowRange,0,1000,p+".followRange");N(actor.LeashRange,0,1000,p+".leashRange");N(actor.PatrolRadius,0,1000,p+".patrolRadius");
                if(actor.Team<0||actor.Team>30)Fail(p+".team","Expected team0..30.");
                if(actor.Slots==null||actor.Slots.Length>3)Fail(p+".slots","Expected up to3 skill slots.");
                if(string.IsNullOrWhiteSpace(actor.View)||Array.IndexOf(new[]{"fighter","stake","melee_ai","melee_guard","ranged_ai","summon"},actor.View)<0)Fail(p+".view","Unknown built-in view key.");
                var definition=new RuntimeActorDefinition{Id=actor.Id,Role=actor.Role,View=actor.View,Team=actor.Team,Hp=actor.Hp,Attack=actor.Attack,Defense=actor.Defense,MoveSpeed=actor.MoveSpeed,
                    Shield=actor.Shield,CritRate=actor.CritRate,DamageDealt=actor.DamageDealt,DamageTaken=actor.DamageTaken,CollisionRadius=actor.CollisionRadius,Energy=actor.Energy,InitialEnergy=actor.Energy,Recovery=actor.Recovery,
                    AcquireRadius=actor.AcquireRadius,AttackRange=actor.AttackRange,FollowRange=actor.FollowRange,LeashRange=actor.LeashRange,PatrolRadius=actor.PatrolRadius,
                    Slots=actor.Slots.Select(id=>Skill(content,id,p+".slots").Id).ToArray(),DodgeSkill=string.IsNullOrEmpty(actor.DodgeSkill)?SkillNodeId.None:Skill(content,actor.DodgeSkill,p+".dodgeSkill").Id};
                if(actor.Role=="ai"||actor.Role=="summon")
                {
                    var skill=Skill(content,actor.SkillId,p+".skillId");definition.Skill=skill.Id;
                    definition.Tree=actor.Ai=="puncher"?BtFactory.MeleePuncher(skill.Id,skill.Timeline):actor.Ai=="guard"?BtFactory.MeleeGuard(skill.Id,skill.Timeline):
                        actor.Ai=="ranged"?BtFactory.Ranged(skill.Id,skill.Timeline):actor.Ai=="summon"?BtFactory.SummonMelee(skill.Id,skill.Timeline):null;
                    if(definition.Tree==null)Fail(p+".ai","Expected puncher, guard, ranged or summon.");
                }
                else if(actor.Ai!="none")Fail(p+".ai","Non-AI role must use none.");
                actors.Add(actor.Id,definition);
            }
            // Shared summon blueprints are the sole source of summon stats in both preview and game.
            foreach(var sourceBlueprint in project.SummonBlueprints)
            {
                content.Blueprints.TryGet(sourceBlueprint.Id,out var b);var skill=Skill(content,b.SkillKey,"$.summonBlueprints["+b.Id+"].skillId");
                if(actors.ContainsKey(b.Id))Fail("$.runtime.actors["+b.Id+"].id","Summon blueprint identity already defines this actor.");
                actors.Add(b.Id,new RuntimeActorDefinition{Id=b.Id,Role="summon",View="summon",Team=1,Hp=b.Hp,Attack=b.Attack,Defense=b.Defense,MoveSpeed=b.MoveSpeed,
                    DamageDealt=1,DamageTaken=1,CollisionRadius=b.CollisionRadius,Energy=b.Energy,InitialEnergy=b.InitialEnergy,Recovery=b.Recovery,
                    Skill=b.Skill,AcquireRadius=b.AcquireRadius,AttackRange=b.AttackRange,FollowRange=b.FollowRange,LeashRange=20,
                    Tree=skill.Mode==SkillCastMode.Instant?BtFactory.SummonMelee(skill.Id,skill.Timeline):SummonBehavior.Create(b.Skill,b.HoldDuration)});
            }
            return new RuntimeContentSnapshot{Data=data,Actors=actors};
        }
        static SkillDefinition Skill(CombatContentSnapshot content,string key,string path)
        {foreach(var skill in content.Skills.All)if(skill.Key==key)return skill;throw new ContentValidationException(path,"Unknown skill: "+key);}
        static void Identity(string id,HashSet<string> keys,string path){if(string.IsNullOrWhiteSpace(id)||!keys.Add(id))Fail(path,"Missing or duplicate identity.");}
        static void N(float v,float min,float max,string path)=>AuthoredTimelineCompiler.Number(v,min,max,path);
        static void Fail(string path,string reason)=>throw new ContentValidationException(path,reason);
    }
}
