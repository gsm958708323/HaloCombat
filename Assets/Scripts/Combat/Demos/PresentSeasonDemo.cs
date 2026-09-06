#if !UNITY_EDITOR
using System;
using Combat.Core;
using Combat.Game;
using Combat.Presentation;

namespace Combat.Demos
{
    public static class PresentSeasonDemo
    {
        public static void Run()
        {
            DemoTables.ResetG1MeleeDefaults();
            PresentSettings.ShowHitboxes = true;
            var loop = new RecordingLoopPort();
            var cues = new RecordingVfxPool { ThrowOnPlay = true };
            var floaters = new RecordingFloaterPool();
            var gizmo = new RecordingGizmoPort();
            using (var session = ArenaSession.StartHeadless())
            {
                var hub = session.Hub;
                hub.Cues.SetPool(cues);
                hub.Floaters.SetPool(floaters);
                if (!hub.TryGet(session.LocalPlayerId, out var playerPresent))
                    throw new Exception("player present");
                playerPresent.Get<BuffFxPresent>().Port = loop;
                playerPresent.Get<HitboxGizmoPresent>().Port = gizmo;
                var pose = playerPresent.Get<PoseFollowPresent>();
                var animation = playerPresent.Get<AnimationPresent>();
                var hud = playerPresent.Get<PlayerHudSourcePresent>();
                var feedback = playerPresent.Get<PlayerFeedbackPresent>();
                var input = GetInput(session.LocalPlayerId, session, out var player);
                var loco = player.GetComp<LocomotionComp>();
                Actor stake = null;
                Actor guard = null;
                foreach (var actor in session.World.RegistryActive())
                {
                    if (actor.Id == player.Id)
                        continue;
                    if (actor.TryGetComp<BehaviorTreeComp>(out _))
                        guard = actor;
                    else if (
                        actor.TryGetComp<TransformComp>(out var transform)
                        && Math.Abs(transform.Position.X - .55f) < .05f
                        && Math.Abs(transform.Position.Z) < .05f
                    )
                        stake = actor;
                }
                if (stake == null || guard == null)
                    throw new Exception("spawn table");
                if (
                    hub.TryGet(guard.Id, out var guardPresent)
                    && guardPresent.TryGet<PlayerCameraPresent>(out _)
                )
                    throw new Exception("guard camera");

                void Step()
                {
                    session.PumpLogic(LogicTicker.Step);
                    session.PumpPresent(LogicTicker.Step);
                }

                loco.RequestMoveIntent(1f, 0f);
                Step();
                if (pose.DisplayPos.X > pose.LogicPos.X + .001f || pose.LogicPos.X <= .01f)
                    throw new Exception("pose extrapolation");
                loco.RequestMoveIntent(0f, 0f);

                input.Push(Season2Tokens.Dodge);
                var sawDodge = false;
                var sawIFrame = false;
                for (int i = 0; i < 10; i++)
                {
                    Step();
                    sawDodge |=
                        animation.Flags.Attack
                        && animation.Flags.SkillId == SkillNodeId.Dodge.Value;
                    sawIFrame |= animation.Flags.IFrame;
                }
                if (!sawDodge || !sawIFrame)
                    throw new Exception("dodge flags");
                for (int i = 0; i < 20; i++)
                    Step();

                var flash = feedback.Flash;
                session.World.Deliver(
                    new IEffect[]
                    {
                        new DamageEffect
                        {
                            Flat = 2,
                            CanCrit = false,
                            HitstopFrames = 0,
                        },
                    },
                    player,
                    stake,
                    0f
                );
                Step();
                if (feedback.Flash > flash + .05f || floaters.DamageFloaters < 1)
                    throw new Exception("local feedback filter");

                var hpBefore = hud.Snapshot.Hp;
                session.World.Deliver(
                    new IEffect[]
                    {
                        new DamageEffect
                        {
                            Coeff = .2f,
                            CanCrit = false,
                            HitstopFrames = 0,
                        },
                    },
                    stake,
                    player,
                    10f
                );
                Step();
                if (feedback.Flash < .5f || hud.Snapshot.Hp >= hpBefore)
                    throw new Exception("local hurt feedback");

                var playCount = loop.PlayCount;
                var stopCount = loop.StopCount;
                session.World.Deliver(
                    new IEffect[] { new ApplyDurationEffect(CombatCatalog.Burn()) },
                    player,
                    player,
                    0f
                );
                Step();
                if (loop.PlayCount <= playCount || hud.Snapshot.BurnStacks < 1)
                    throw new Exception("buff play");
                session.World.Deliver(
                    new IEffect[] { new DispelEffect(DispelMode.ByBuffId, CombatIds.Burn) },
                    player,
                    player,
                    0f
                );
                Step();
                if (loop.StopCount <= stopCount || hud.Snapshot.BurnStacks != 0)
                    throw new Exception("buff stop");

                input.Push(InputToken.Attack);
                var gizmoSeen = false;
                for (int i = 0; i < 16; i++)
                {
                    Step();
                    if (
                        player.TryGetComp<HitboxComp>(out var box)
                        && box.IsOpen
                        && player.TryGetComp<TransformComp>(out var transform)
                        && gizmo.Last.Visible
                    )
                    {
                        var expected = CombatGeom.HitboxCenter(transform, box);
                        var dx = gizmo.Last.Center.X - expected.X;
                        var dz = gizmo.Last.Center.Z - expected.Z;
                        gizmoSeen |=
                            dx * dx + dz * dz < 1e-6f
                            && Math.Abs(gizmo.Last.Radius - box.Radius) < 1e-4f;
                    }
                }
                if (!gizmoSeen)
                    throw new Exception("gizmo geometry");

                var gotHitstop = false;
                var hitstopFrame = session.Frame;
                var hitstopPosition = player.GetComp<TransformComp>().Position;
                for (int i = 0; i < 12; i++)
                {
                    Step();
                    if (session.World.InHitstop)
                    {
                        gotHitstop = true;
                        hitstopFrame = session.Frame;
                        hitstopPosition = player.GetComp<TransformComp>().Position;
                        break;
                    }
                }
                if (gotHitstop)
                {
                    for (int i = 0; i < 2 && session.World.InHitstop; i++)
                    {
                        Step();
                        var now = player.GetComp<TransformComp>().Position;
                        if (Math.Abs(now.X - hitstopPosition.X) > 1e-4f)
                            throw new Exception("hitstop movement");
                    }
                    if (session.Frame < hitstopFrame)
                        throw new Exception("hitstop frame");
                }

                for (int i = 0; i < 30; i++)
                    Step();
                var pausedFrame = session.Frame;
                session.TogglePause();
                session.PumpLogic(.5f);
                session.PumpPresent(.5f);
                if (session.Frame != pausedFrame)
                    throw new Exception("pause frame");
                session.TogglePause();

                session.World.Events.Publish(new EvCue(99999, session.LocalPlayerId, "missing"));
                cues.ThrowOnPlay = false;

                session.World.Deliver(
                    new IEffect[] { new KnockdownEffect { Duration = .35f } },
                    stake,
                    player,
                    0f
                );
                Step();
                if (!animation.Flags.Downed)
                    throw new Exception("downed flags");
                player
                    .GetComp<StateMachineComp>()
                    .TryEnter(ActivityId.Dead, new ActivityEnterArgs { Reason = "s3s" });
                if (!animation.DeathPlayed)
                    throw new Exception("death presentation");
            }

            using (var second = ArenaSession.StartHeadless())
            {
                if (second.Hub.Count < 2)
                    throw new Exception("replay");
            }
            PresentSettings.ShowHitboxes = false;
            Console.WriteLine("PresentSeasonDemo PASSED");
        }

        static InputBufferComp GetInput(EntityId id, ArenaSession session, out Actor actor)
        {
            if (!session.World.TryGetActor(id, out actor) || actor == null)
                throw new Exception("player actor");
            return actor.GetComp<InputBufferComp>();
        }
    }
}
#endif
