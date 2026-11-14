using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class TimelineIntervalTests
    {
        sealed class Factory : IActorFactory
        {
            public TimelineLibrary Library;
            public Actor Create(in ActorSpawnSpec spec)
            {
                var a=new Actor();a.AddComp(new TransformComp());a.AddComp(new TagComp());a.AddComp(new TeamComp(1));
                var attributes=new AttributeSet();attributes.InitFighterDefaults();a.AddComp(attributes);
                a.AddComp(new StateMachineComp());a.AddComp(new LocomotionComp());a.AddComp(new SkillDirectorComp(Library));return a;
            }
            public void Release(Actor a)=>a.ResetForPool();
        }
        [Test]
        public void AStepCrossingBothClipBoundariesOnlyIntegratesTheClipInterval()
        {
            var library=new TimelineLibrary();library.Register(new TimelineDefinition {Id=new TimelineId(777),Duration=.3f,
                Clips=new[]{new TimelineClip {Start=.1f,End=.2f,Kind=ClipKind.Move,MoveX=6}}});
            var world=new CombatWorld(new Factory {Library=library});var id=world.SpawnActor(new ActorSpawnSpec("caster"));world.TryGetActor(id,out var actor);
            actor.GetComp<SkillDirectorComp>().Play(new SkillNodeId(777),new TimelineId(777));world.Tick(.25f);
            Assert.AreEqual(6,actor.GetComp<TransformComp>().Position.X,.0001f);
            world.Tick(.05f);Assert.AreEqual(6,actor.GetComp<TransformComp>().Position.X,.0001f);
        }
    }
}
