using System;
using System.Collections;
using Combat.Core;
using Combat.Unity.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Combat.Tests
{
    /// <summary>
    /// Drives the real Arena session and asserts that every fire key produces the body it is
    /// supposed to. This is the guard for the class of bug where a baked effect lost its
    /// configuration (for example a projectile spec id of 0, or an effect that deserialised as
    /// null), so every skill cast but spawned nothing while the game reported no errors at all.
    /// </summary>
    public sealed class BuffArenaSkillSmokeTests
    {
        const float Step = 1f / 50f;
        const int TicksPerSkill = 70;
        const float EnemyTestHp = 100f;

        BuffArenaBootstrap _bootstrap;
        BuffArenaSession _session;

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            // The editor throttles the player loop while its window is not focused, which would
            // stall an unattended run before the first frame.
            Application.runInBackground = true;

            SceneManager.LoadScene("Arena");
            yield return null;

            _bootstrap = UnityEngine.Object.FindObjectOfType<BuffArenaBootstrap>();
            Assert.IsNotNull(_bootstrap, "Arena scene has no BuffArenaBootstrap.");
            _session = _bootstrap.Session;
            Assert.IsNotNull(
                _session,
                "BuffArenaSession did not start; the generated content is unusable: "
                    + (_bootstrap.Database != null
                        ? _bootstrap.Database.LastContentError
                        : "the bootstrap has no database assigned"));
            ClearNonPlayerActors();
        }

        [UnityTest]
        public IEnumerator Arena_RunsOnGeneratedContent()
        {
            Assert.IsNotNull(_bootstrap.Database, "Arena bootstrap has no database assigned.");
            Assert.IsTrue(
                _bootstrap.Database.UseGeneratedContent,
                "Arena is not running on generated content, so these tests would not cover the asset path.");
            var content = _bootstrap.Database.BakeContent();
            Assert.IsNotNull(
                content,
                "Generated content did not bake: " + _bootstrap.Database.LastContentError);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire1_SpawnsProjectile()
        {
            int ammoBefore = PlayerAmmo();
            int peak = PressAndWatch<ProjectileComp>(f => { f.Fire1Held = true; return f; });
            Assert.Greater(peak, 0, "Fire1 (skill 1) spawned no projectile.");
            Assert.Less(PlayerAmmo(), ammoBefore, "Fire1 did not spend ammo, so the skill never ran.");
            yield return null;
        }

        /// <summary>
        /// The regression this guards: the projectile spawned and flew, but its hitbox payload had
        /// been baked with null effects, so it passed straight through the enemy. Spawning the
        /// target on the player's own facing keeps the hit independent of aim timing.
        /// </summary>
        [UnityTest]
        public IEnumerator Fire1_DamagesEnemyInFront()
        {
            var enemy = SpawnEnemyInFront(1.2f);
            float before = Hp(enemy);
            Assert.Greater(before, 0f, "The test enemy spawned with no health, so damage cannot be observed.");
            int ammoBefore = PlayerAmmo();

            Press(f => { f.Fire1Held = true; return f; });

            Assert.Less(PlayerAmmo(), ammoBefore, "Fire1 did not spend ammo, so the skill never ran.");
            float after = Hp(enemy);
            Assert.Less(
                after, before,
                "Fire1 spawned a projectile that dealt no damage: the enemy's health stayed at " + before
                    + ". Its hitbox payload is baked with null effects, so the hit does nothing.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire2_SpawnsProjectile()
        {
            int peak = PressAndWatch<ProjectileComp>(f => { f.Fire2Held = true; return f; });
            Assert.Greater(peak, 0, "Fire2 (skill 2) spawned no projectile.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire3_SpawnsProjectile()
        {
            int peak = PressAndWatch<ProjectileComp>(f => { f.Fire3Held = true; return f; });
            Assert.Greater(peak, 0, "Fire3 (skill 3) spawned no projectile.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire4_SpawnsProjectile()
        {
            int peak = PressAndWatch<ProjectileComp>(f => { f.Fire4Held = true; return f; });
            Assert.Greater(peak, 0, "Fire4 (skill 4) spawned no projectile.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire5_SpawnsBarrel()
        {
            int peak = PressAndWatch<BarrelComp>(f => { f.Fire5Held = true; return f; });
            Assert.Greater(peak, 0, "Fire5 (skill 5) spawned no barrel.");
            yield return null;
        }

        /// <summary>
        /// Pushes one input frame and lets the logic advance past the timeline.
        /// BuffArenaInputFrame is a struct, so an Action&lt;T&gt; lambda would mutate a copy and
        /// send an all-empty frame — every press has to hand its frame back out.
        /// </summary>
        void Press(Func<BuffArenaInputFrame, BuffArenaInputFrame> configure)
        {
            _session.ApplyInput(configure(new BuffArenaInputFrame()));
            for (int i = 0; i < TicksPerSkill; i++)
                _session.PumpLogic(Step);
        }

        /// <summary>
        /// Presses a key and reports the highest number of bodies of the given comp seen while the
        /// timeline ran: a projectile that already flew off or expired still counts as spawned.
        /// </summary>
        int PressAndWatch<T>(Func<BuffArenaInputFrame, BuffArenaInputFrame> configure) where T : Comp
        {
            _session.ApplyInput(configure(new BuffArenaInputFrame()));
            int peak = CountBodies<T>();
            for (int i = 0; i < TicksPerSkill; i++)
            {
                _session.PumpLogic(Step);
                int now = CountBodies<T>();
                if (now > peak) peak = now;
            }
            return peak;
        }

        int CountBodies<T>() where T : Comp
        {
            int count = 0;
            var actors = _session.World.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] == null) continue;
                if (actors[i].TryGetComp<T>(out _)) count++;
            }
            return count;
        }

        /// <summary>
        /// Spawns one enemy straight ahead of the player at the given distance, which is where a
        /// skill 1 shot travels, and gives it a known health pool.
        /// </summary>
        Actor SpawnEnemyInFront(float distance)
        {
            Assert.IsTrue(
                _session.World.TryGetActor(_session.LocalPlayerId, out var player) && player != null,
                "The local player is gone, so no shot origin exists.");
            var playerTf = player.GetComp<TransformComp>();
            var forward = LocomotionComp.ForwardFromYaw(playerTf.YawDegrees);
            var id = _session.World.SpawnActor(
                new ActorSpawnSpec(BuffArenaIds.EnemyBlueprint), publishSpawn: false);
            Assert.IsTrue(
                _session.World.TryGetActor(id, out var enemy) && enemy != null,
                "The test enemy could not be spawned from the generated actor table.");
            var origin = playerTf.Position;
            enemy.GetComp<TransformComp>().Position = new SimVec3(
                origin.X + forward.X * distance, origin.Y, origin.Z + forward.Z * distance);
            var attr = enemy.GetComp<AttributeSet>();
            attr.SetBase(AttrId.MaxHp, EnemyTestHp);
            attr.SetBase(AttrId.Hp, EnemyTestHp);
            return enemy;
        }

        float Hp(Actor actor)
        {
            if (actor == null) return 0f;
            return actor.TryGetComp<AttributeSet>(out var attr) ? attr.GetBase(AttrId.Hp) : 0f;
        }

        int PlayerAmmo()
        {
            if (!_session.World.TryGetActor(_session.LocalPlayerId, out var player) || player == null)
                return -1;
            return player.TryGetComp<AmmoComp>(out var ammo) ? ammo.Current : -1;
        }

        /// <summary>Removes the wandering enemies so only the player's own skills are counted.</summary>
        void ClearNonPlayerActors()
        {
            var actors = _session.World.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor == null || actor.Id == _session.LocalPlayerId) continue;
                _session.World.RequestDespawn(actor.Id);
            }
            _session.PumpLogic(Step);
        }
    }
}
