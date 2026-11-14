using System;
using System.Collections.Generic;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class EventBusReentrancyTests
    {
        [Test]
        public void NestedPublicationDoesNotReplaceTheRemainingOriginalListeners()
        {
            var bus=new EventBus();var calls=new List<string>();
            bus.Subscribe<int>(n=>{calls.Add("first:"+n);bus.Publish("nested");});bus.Subscribe<int>(n=>calls.Add("second:"+n));bus.Subscribe<string>(s=>calls.Add(s));
            bus.Publish(3);CollectionAssert.AreEqual(new[]{"first:3","nested","second:3"},calls);
        }
        [Test]
        public void SubscriptionChangesTakeEffectOnTheNextPublicationEvenWithSameTypeReentry()
        {
            var bus=new EventBus();var calls=new List<int>();Action<int> second=n=>calls.Add(n*10);Action<int> added=n=>calls.Add(n*100);
            bus.Subscribe<int>(n=>{calls.Add(n);if(n==1){bus.Unsubscribe(second);bus.Subscribe(added);bus.Publish(2);}});bus.Subscribe(second);
            bus.Publish(1);CollectionAssert.AreEqual(new[]{1,2,200,10},calls);
        }
    }
}
