using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class ComponentLifecycleTests
    {
        sealed class Probe : Comp
        {
            public int Detaches;
            protected override void OnDetach()=>Detaches++;
        }
        [Test]
        public void RegistryDetachFollowedByFactoryPoolResetOnlyDetachesEachAttachmentOnce()
        {
            var actor=new Actor();var probe=new Probe();actor.AddComp(probe);actor.AttachAll();actor.DetachAll();actor.ResetForPool();Assert.AreEqual(1,probe.Detaches);
            actor.AddComp(probe);actor.AttachAll();actor.ResetForPool();Assert.AreEqual(2,probe.Detaches);
        }
    }
}
