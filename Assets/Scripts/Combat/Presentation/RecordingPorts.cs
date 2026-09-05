using System.Collections.Generic;
using Combat.Core;

namespace Combat.Presentation
{
    public sealed class RecordingLoopPort : ILoopVfxPort
    {
        public int PlayCount,
            StopCount,
            LastVisualId,
            LastStacks;
        int _next = 1;

        public int PlayLoop(int id, EntityId f)
        {
            PlayCount++;
            LastVisualId = id;
            return _next++;
        }

        public void SetIntensity(int h, int s) => LastStacks = s;

        public void Stop(int h) => StopCount++;
    }

    public sealed class RecordingVfxPool : IVfxPool
    {
        public readonly List<int> PlayedCueIds = new List<int>();
        public int PlayCalls;
        public bool ThrowOnPlay;

        public bool TryPlay(in CueDef d, SimVec3 p, EntityId s)
        {
            if (ThrowOnPlay)
                throw new System.InvalidOperationException("Cue must not throw");
            PlayCalls++;
            PlayedCueIds.Add(d.CueId);
            return true;
        }

        public void TickUnscaled(float d) { }

        public void ReturnAll()
        {
            PlayedCueIds.Clear();
        }
    }

    public sealed class RecordingFloaterPool : IFloaterPool
    {
        public int DamageFloaters,
            ImmuneFloaters;
        public float LastAmount;
        public EntityId LastTarget;

        public bool TryPlay(in FloaterRequest r)
        {
            LastAmount = r.Amount;
            LastTarget = r.Target;
            if (r.Immune)
                ImmuneFloaters++;
            else if (!r.Heal)
                DamageFloaters++;
            return true;
        }

        public void TickUnscaled(float d) { }

        public void ReturnAll() { }
    }

    public sealed class RecordingGizmoPort : IGizmoDrawPort
    {
        public GizmoFrame Last;
        public int DrawCalls;

        public void DrawCircle(in GizmoFrame f)
        {
            Last = f;
            DrawCalls++;
        }

        public void Clear() { }
    }
}
