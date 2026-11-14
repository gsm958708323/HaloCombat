using Combat.Core;
using Combat.Presentation;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class PresentationLifecycleTests
    {
        sealed class View : IActorView
        {
            public EntityId Id {get;private set;}
            public int Deaths,Releases;
            public void Bind(EntityId id,string blueprint)=>Id=id;
            public void Sample(in PoseSample sample){}
            public void OnDead(in EvEntityDead e)=>Deaths++;
            public void Release()=>Releases++;
        }
        sealed class Factory : IViewFactory
        {
            public View Last;
            public IActorView Create(string blueprint)=>Last=new View();
        }
        sealed class Overlay : IHitstopOverlay
        {
            public int Flashes;
            public void ShowFlash()=>Flashes++;
            public void SetActive(bool on){}
        }
        [Test]
        public void OldGenerationDeathAndCleanupCannotChangeTheReplacementView()
        {
            var factory=new Factory();var views=new ViewRegistry(factory);var old=new EntityId(4,1);var current=new EntityId(4,2);
            views.HandleSpawn(new EvEntitySpawn(old,"player",EntityId.Invalid));var original=factory.Last;
            views.HandleSpawn(new EvEntitySpawn(current,"player",EntityId.Invalid));var replacement=factory.Last;Assert.AreEqual(1,original.Releases);
            views.HandleDead(new EvEntityDead(old,EntityId.Invalid));views.HandleCleanup(new EvEntityCleanup(old,"old"));Assert.AreEqual(0,replacement.Deaths);Assert.AreEqual(0,replacement.Releases);
            views.HandleDead(new EvEntityDead(current,EntityId.Invalid));views.HandleCleanup(new EvEntityCleanup(current,"current"));Assert.AreEqual(1,replacement.Deaths);Assert.AreEqual(1,replacement.Releases);
        }
        [Test]
        public void UnbindingRouterStopsHitstopCallbacksBeforeTheNextSessionBinds()
        {
            var bus=new EventBus();var overlay=new Overlay();var router=new PresentationRouter(null,new ViewRegistry(new Factory()),hitstop:overlay);
            var hit=new EvHitstop(EntityId.Invalid,EntityId.Invalid,3);router.Bind(bus);bus.Publish(hit);router.Unbind(bus);bus.Publish(hit);Assert.AreEqual(1,overlay.Flashes);
            router.Bind(bus);bus.Publish(hit);Assert.AreEqual(2,overlay.Flashes);router.Unbind(bus);
        }
    }
}
