using System.Collections;
using Combat.Core;
using Combat.Unity.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Combat.Tests
{
    public sealed partial class BuffArenaSkillSmokeTests
    {
        Actor Player()
        {
            Assert.IsTrue(_session.World.TryGetActor(_session.LocalPlayerId, out var player));
            return player;
        }

        [UnityTest] public IEnumerator PressPriorityAndHeldPrimaryDoNotLoseSkillCommands()
        {
            var player = Player(); var q = player.GetComp<InputBufferComp>();
            _session.ApplyInput(new BuffArenaInputFrame { RollPressed = true, Fire4Pressed = true,
                MonkeyPressed = true, Fire1Held = true });
            Assert.AreEqual(3, q.Count);
            foreach (var expected in new[] { BuffArenaIds.RollInput, BuffArenaIds.Fire4, BuffArenaIds.MonkeyInput })
            {
                Assert.IsTrue(q.TryPeek(out var token)); Assert.AreEqual(expected, token); q.Consume();
            }
            _session.ApplyInput(new BuffArenaInputFrame { Fire1Held = true, Fire5Pressed = true });
            _session.PumpLogic(Step);
            Assert.AreEqual(2008, PlayerDirector().CurrentSkill.Value);
            for (int i = 0; i < 30; i++) _session.PumpLogic(Step);
            Assert.AreEqual(2001, PlayerDirector().CurrentSkill.Value, "Held primary resumes only after queued skill.");
            _session.ApplyInput(default);
            yield return null;
        }

        [UnityTest] public IEnumerator SkillBlockRetainsQueueAndOnePressDoesNotRepeat()
        {
            var player = Player(); var q = player.GetComp<InputBufferComp>();
            var block = player.GetComp<TagComp>().Acquire(CommonTags.BlockSkill);
            _session.ApplyInput(new BuffArenaInputFrame { Fire5Pressed = true });
            for (int i = 0; i < 5; i++) _session.PumpLogic(Step);
            Assert.AreEqual(1, q.Count); Assert.IsFalse(PlayerDirector().IsPlaying);
            block.Release();
            int spawned = 0;
            System.Action<EvEntitySpawn> count = e => { if (e.BlueprintId == BuffArenaIds.BarrelBlueprint) spawned++; };
            _session.World.Events.Subscribe(count);
            for (int i = 0; i < 90; i++) { _session.ApplyInput(default); _session.PumpLogic(Step); }
            _session.World.Events.Unsubscribe(count);
            Assert.AreEqual(1, spawned);
            yield return null;
        }

        [UnityTest] public IEnumerator ActualInputSamplingSeparatesPressAndHold()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var source = new BuffArenaInputSource(_bootstrap.Actions, null);
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1, Key.Digit3));
                InputSystem.Update();
                var first = source.Sample(Vector3.zero);
                Assert.IsTrue(first.Fire1Pressed && first.Fire1Held && first.Fire3Pressed);
                InputSystem.Update();
                var held = source.Sample(Vector3.zero);
                Assert.IsTrue(held.Fire1Held); Assert.IsFalse(held.Fire1Pressed || held.Fire3Pressed);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Assert.IsFalse(source.Sample(Vector3.zero).Fire1Held);
            }
            finally { source.Dispose(); InputSystem.RemoveDevice(keyboard); }
            yield return null;
        }

        [UnityTest] public IEnumerator F5RestartsAliveFrozenAndDeadSessionsWithoutDuplicates()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                for (int pass = 0; pass < 3; pass++)
                {
                    var old = _bootstrap; var oldSession = _session;
                    var player = Player();
                    if (pass == 1) { player.Time.RequestHitstop(100); _session.PumpLogic(Step); }
                    if (pass == 2) player.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead, default);
                    player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire5);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F5));
                    float deadline = Time.realtimeSinceStartup + 15f;
                    while (old != null && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.IsTrue(old == null, "F5 must restart even when the session cannot simulate.");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null;
                    _bootstrap = Object.FindObjectOfType<BuffArenaBootstrap>();
                    Assert.IsNotNull(_bootstrap);
                    _session = _bootstrap.Session;
                    Assert.IsNotNull(_session); Assert.IsNull(oldSession.World);
                    Assert.AreEqual(1, Object.FindObjectsOfType<BuffArenaBootstrap>().Length);
                    Assert.IsFalse(_session.PlayerDead); Assert.IsFalse(Player().Time.IsStopped);
                    Assert.AreEqual(0, Player().GetComp<InputBufferComp>().Count);
                    Assert.AreEqual(Player().GetComp<AmmoComp>().Capacity, PlayerAmmo());
                    int players = 0;
                    foreach (var a in _session.World.RegistryActive())
                        if (a.BlueprintId == BuffArenaIds.PlayerBlueprint) players++;
                    Assert.AreEqual(1, players);
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
    }
}
