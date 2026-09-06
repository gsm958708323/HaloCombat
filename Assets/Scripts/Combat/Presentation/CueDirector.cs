using Combat.Core;

namespace Combat.Presentation
{
    public interface IVfxPool
    {
        bool TryPlay(in CueDef def, SimVec3 logicPos, EntityId source);
        void TickUnscaled(float dt);
        void ReturnAll();
    }

    public sealed class NullVfxPool : IVfxPool
    {
        public bool TryPlay(in CueDef d, SimVec3 p, EntityId s) => true;

        public void TickUnscaled(float d) { }

        public void ReturnAll() { }
    }

    public sealed class CueDirector
    {
        CueLibrary _lib;
        IVfxPool _pool = new NullVfxPool();
        CombatWorld _world;

        public void SetLibrary(CueLibrary l) => _lib = l;

        public void SetPool(IVfxPool p) => _pool = p ?? new NullVfxPool();

        public void SetWorld(CombatWorld w) => _world = w;

        public void OnCue(EvCue e)
        {
            if (
                _lib == null
                || !_lib.TryGet(e.CueId, out var d)
                || _world == null
                || !_world.TryGetActor(e.Source, out var a)
                || !a.TryGetComp<TransformComp>(out var tf)
            )
                return;
            // Presentation resources are optional and must never interrupt simulation.
            try
            {
                _pool.TryPlay(d, tf.Position, e.Source);
            }
            catch
            {
                // A missing or faulty cue is a safe visual no-op.
            }
        }

        public void Pump(float dt) => _pool.TickUnscaled(dt);

        public void ReturnAll() => _pool.ReturnAll();
    }
}
