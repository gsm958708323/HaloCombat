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
            PresentSettings.ShowHitboxes = true;
            var loop = new RecordingLoopPort();
            var floaters = new RecordingFloaterPool();
            var session = ArenaSession.StartHeadless();
            try
            {
                session.Hub.Floaters.SetPool(floaters);
                if (!session.Hub.TryGet(session.LocalPlayerId, out var p))
                    throw new Exception("player present");
                if (p.TryGet<BuffFxPresent>(out var fx))
                    fx.Port = loop;
                if (p.TryGet<HitboxGizmoPresent>(out var gz))
                    gz.Port = new RecordingGizmoPort();
                session.PumpLogic(LogicTicker.Step);
                session.PumpPresent(LogicTicker.Step);
                if (!p.TryGet<PlayerHudSourcePresent>(out var hud) || !hud.Snapshot.Valid)
                    throw new Exception("hud snapshot");
                if (!p.TryGet<AnimationPresent>(out var anim))
                    throw new Exception("animation present");
                session.World.TryGetActor(session.LocalPlayerId, out var player);
                var stakeId = EntityId.Invalid;
                foreach (var a in session.World.RegistryActive())
                    if (
                        a.Id != session.LocalPlayerId
                        && a.TryGetComp<TeamComp>(out var t)
                        && t.TeamId == 1
                    )
                    {
                        stakeId = a.Id;
                        break;
                    }
                if (stakeId.IsValid)
                {
                    session.World.Deliver(
                        new IEffect[]
                        {
                            new DamageEffect { Flat = 2, CanCrit = false },
                        },
                        player,
                        player,
                        0
                    );
                    session.PumpLogic(LogicTicker.Step);
                    if (floaters.DamageFloaters < 1)
                        throw new Exception("floater");
                    session.World.Deliver(
                        new IEffect[] { new ApplyDurationEffect(CombatCatalog.Burn()) },
                        player,
                        player,
                        0
                    );
                    session.PumpLogic(LogicTicker.Step);
                    if (loop.PlayCount < 1)
                        throw new Exception("buff loop");
                }
                int frame = session.Frame;
                session.TogglePause();
                session.PumpLogic(.5f);
                if (session.Frame != frame)
                    throw new Exception("pause frame");
                session.TogglePause();
            }
            finally
            {
                session.Dispose();
                PresentSettings.ShowHitboxes = false;
            }
            using (var again = ArenaSession.StartHeadless())
            {
                if (again.Hub.Count < 2)
                    throw new Exception("replay");
            }
            Console.WriteLine("PresentSeasonDemo PASSED");
        }
    }
}
#endif
