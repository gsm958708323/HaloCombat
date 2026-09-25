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

        // The Arena takes the player's facing from the mouse ray, so a test that wants a
        // deterministic shot has to place the target on an aim yaw it can pin for the cast.
        float _aimYaw;

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
        public IEnumerator Arena_ContentBakes()
        {
            Assert.IsNotNull(_bootstrap.Database, "Arena bootstrap has no database assigned.");
            var content = _bootstrap.Database.Bake();
            Assert.IsNotNull(
                content,
                "The authored content did not bake: " + _bootstrap.Database.LastContentError);
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
        /// been baked with null effects, so it passed straight through the enemy. The target goes on
        /// a pinned aim yaw, because the Arena follows the mouse and the shot would otherwise fly
        /// wherever the cursor happens to point.
        /// </summary>
        [UnityTest]
        public IEnumerator Fire1_DamagesEnemyInFront()
        {
            var enemy = SpawnEnemyInFront(1.2f);
            float before = Hp(enemy);
            Assert.Greater(before, 0f, "The test enemy spawned with no health, so damage cannot be observed.");
            int ammoBefore = PlayerAmmo();

            Press(f => { f.Fire1Held = true; f.AimValid = true; f.AimYaw = _aimYaw; return f; });

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

        /// <summary>
        /// Every key in BuffArenaSession.ApplyInput has to cast the skill declaring the token it
        /// pushes. The key map is code-owned; the token -> skill half is authored on SK_*.asset.
        /// The CLI demo that used to cover roll / homing / monkey is gone, so this is their guard.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryKeyCastsItsSkill()
        {
            AssertKeyCasts("Fire1", f => { f.Fire1Held = true; return f; });
            AssertKeyCasts("Fire2", f => { f.Fire2Held = true; return f; });
            AssertKeyCasts("Fire3", f => { f.Fire3Held = true; return f; });
            AssertKeyCasts("Fire4", f => { f.Fire4Held = true; return f; });
            AssertKeyCasts("Fire5", f => { f.Fire5Held = true; return f; });
            AssertKeyCasts("Roll", f => { f.RollHeld = true; return f; });
            AssertKeyCasts("Homing", f => { f.HomingHeld = true; return f; });
            AssertKeyCasts("Monkey", f => { f.MonkeyHeld = true; return f; });
            yield return null;
        }

        void AssertKeyCasts(string token, Func<BuffArenaInputFrame, BuffArenaInputFrame> configure)
        {
            var data = _session.Data;
            Assert.IsNotNull(data, "The session has no baked data.");
            var expected = FindSkillByToken(data, new InputToken(token));
            Assert.IsNotNull(
                expected,
                "No skill asset declares InputToken '" + token + "', so the bound key would do nothing.");

            RefillPlayerAmmo();
            WaitForDirectorIdle();

            _session.ApplyInput(configure(new BuffArenaInputFrame()));
            for (int i = 0; i < 3; i++) _session.PumpLogic(Step);

            var director = PlayerDirector();
            Assert.IsNotNull(director, "The player has no SkillDirectorComp.");
            Assert.IsTrue(
                director.IsPlaying,
                "The key that pushes token '" + token + "' started no skill.");
            Assert.AreEqual(
                expected.Id.Value, director.CurrentSkill.Value,
                "The key that pushes token '" + token + "' started skill "
                    + director.CurrentSkill.Value + " instead of " + expected.Id.Value + ".");
            WaitForDirectorIdle();
        }

        static BuffArenaSkill FindSkillByToken(BuffArenaData data, InputToken token)
        {
            for (int i = 0; i < data.Skills.Count; i++)
                if (data.Skills[i].Input == token) return data.Skills[i];
            return null;
        }

        SkillDirectorComp PlayerDirector()
        {
            if (!_session.World.TryGetActor(_session.LocalPlayerId, out var player) || player == null)
                return null;
            return player.TryGetComp<SkillDirectorComp>(out var director) ? director : null;
        }

        void WaitForDirectorIdle()
        {
            for (int i = 0; i < 200; i++)
            {
                var director = PlayerDirector();
                if (director == null || !director.IsPlaying) return;
                _session.PumpLogic(Step);
            }
        }

        void RefillPlayerAmmo()
        {
            if (!_session.World.TryGetActor(_session.LocalPlayerId, out var player) || player == null) return;
            if (player.TryGetComp<AmmoComp>(out var ammo)) ammo.Refill(ammo.Capacity);
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
        /// Spawns one enemy with a known health pool on the first direction from the player that the
        /// map accepts, pins the shot to that yaw, and freezes the target's AI so it cannot wander
        /// out of the line of fire. The player starts on a random free cell, so straight ahead can
        /// be a wall; without the sweep the projectile would stop short and the test would read as
        /// "the hit does nothing".
        /// </summary>
        Actor SpawnEnemyInFront(float distance)
        {
            Assert.IsTrue(
                _session.World.TryGetActor(_session.LocalPlayerId, out var player) && player != null,
                "The local player is gone, so no shot origin exists.");
            var playerTf = player.GetComp<TransformComp>();
            var origin = playerTf.Position;
            float radius = player.GetComp<CharacterRadiusComp>().Radius;

            var target = new SimVec3();
            bool placed = false;
            for (int i = 0; i < 12 && !placed; i++)
            {
                float yaw = playerTf.YawDegrees + i * 30f;
                var forward = LocomotionComp.ForwardFromYaw(yaw);
                var candidate = new SimVec3(
                    origin.X + forward.X * distance, origin.Y, origin.Z + forward.Z * distance);
                if (!_session.Map.CanPlace(candidate, radius, false)) continue;
                _aimYaw = yaw;
                target = candidate;
                placed = true;
            }
            Assert.IsTrue(placed, "No clear line of fire around the player for the damage test.");

            var id = _session.World.SpawnActor(
                new ActorSpawnSpec(BuffArenaIds.EnemyBlueprint), publishSpawn: false);
            Assert.IsTrue(
                _session.World.TryGetActor(id, out var enemy) && enemy != null,
                "The test enemy could not be spawned from the generated actor table.");
            enemy.GetComp<TransformComp>().Position = target;
            // This test is about the hit landing, not about enemy AI, so the target stands still.
            if (enemy.TryGetComp<BehaviorTreeComp>(out var ai))
                ai.SetEnabled(false);
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

        /// <summary>
        /// Removes the wandering enemies so only the player's own skills are counted.
        /// The session spawns its first wave on its first logic step, so the removal runs after a
        /// pump and is repeated once. Otherwise a leftover enemy standing point-blank could eat a
        /// projectile on the very frame it spawns, which per-tick sampling cannot observe.
        /// </summary>
        void ClearNonPlayerActors()
        {
            for (int pass = 0; pass < 2; pass++)
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
}
