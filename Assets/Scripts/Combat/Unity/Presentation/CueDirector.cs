using System;
using Combat.Core;

namespace Combat.Unity.Presentation
{
    public interface IVfxPool
    {
        bool TryPlay(in CueDef def, SimVec3 logicPos, EntityId source);
        void TickUnscaled(float dt);
        void ReturnAll();
    }

    public interface IAdvancedVfxPool : IVfxPool
    {
        bool TryPlay(in CueDef def, in EvCue cue, SimVec3 logicPos);
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
        Func<EntityId, string, SimVec3?> _anchorResolver;

        public void SetLibrary(CueLibrary l) => _lib = l;

        public void SetPool(IVfxPool p) => _pool = p ?? new NullVfxPool();

        public void SetAnchorResolver(Func<EntityId, string, SimVec3?> resolver)
            => _anchorResolver = resolver;

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
            SimVec3 position = e.HasPoint ? e.Point : tf.Position;
            EntityId anchorId = e.Target.IsValid ? e.Target : e.Source;
            if (!e.HasPoint && !string.IsNullOrEmpty(e.AnchorKey) && _anchorResolver != null)
            {
                var anchored = _anchorResolver(anchorId, e.AnchorKey);
                if (anchored.HasValue) position = anchored.Value;
            }

            if (_pool is IAdvancedVfxPool advanced)
                advanced.TryPlay(d, e, position);
            else
                _pool.TryPlay(d, position, e.Source);
        }

        public void Pump(float dt) => _pool.TickUnscaled(dt);

        public void ReturnAll() => _pool.ReturnAll();
    }
}
