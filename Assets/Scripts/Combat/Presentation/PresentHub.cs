using System;
using System.Collections.Generic;
using Combat.Core;

namespace Combat.Presentation
{
    public interface IPresentFactory
    {
        PresentActor Create(string blueprintId);
        void Release(PresentActor actor);
    }

    public sealed class PresentHub
    {
        readonly Dictionary<long, PresentActor> _map = new Dictionary<long, PresentActor>(64);
        readonly List<PresentActor> _scratch = new List<PresentActor>(64);
        readonly IPresentFactory _factory;
        readonly CueDirector _cues = new CueDirector();
        readonly FloaterLayer _floaters = new FloaterLayer();
        EntityId _local;
        EventBus _bus;
        Action<EvCue> _cue;
        Action<EvDamage> _damage;
        Action<EvImmune> _immune;
        Action<EvHeal> _heal;
        public int Count => _map.Count;
        public CueDirector Cues => _cues;
        public FloaterLayer Floaters => _floaters;
        public EntityId LocalId => _local;

        public void CopyActors(List<PresentActor> destination)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            foreach (var pair in _map)
                destination.Add(pair.Value);
        }

        public PresentHub(IPresentFactory f) => _factory = f ?? throw new ArgumentNullException(nameof(f));

        public void SetWorld(CombatWorld w)
        {
            _cues.SetWorld(w);
            _floaters.SetWorld(w);
            if (w != null)
                _cues.SetLibrary(w.Cues);
        }

        public void SetLocalPlayer(EntityId id) => _local = id;

        public void SetPaused(bool paused)
        {
            if (TryGet(_local, out var p) && p.TryGet<PlayerCameraPresent>(out var c))
                c.SetPaused(paused);
        }

        public void BindBus(EventBus b)
        {
            UnbindBus();
            _bus = b;
            if (b == null)
                return;
            _cue = _cues.OnCue;
            _damage = OnDamage;
            _immune = OnImmune;
            _heal = OnHeal;
            b.Subscribe(_cue);
            b.Subscribe(_damage);
            b.Subscribe(_immune);
            b.Subscribe(_heal);
        }

        public void UnbindBus()
        {
            if (_bus == null)
                return;
            if (_cue != null)
                _bus.Unsubscribe(_cue);
            if (_damage != null)
                _bus.Unsubscribe(_damage);
            if (_immune != null)
                _bus.Unsubscribe(_immune);
            if (_heal != null)
                _bus.Unsubscribe(_heal);
            _bus = null;
            _cue = null;
            _damage = null;
            _immune = null;
            _heal = null;
        }

        public PresentActor BindSpawn(EntityId id, string bp)
        {
            if (!id.IsValid)
                throw new ArgumentException("id");
            long k = HitboxComp.Pack(id);
            if (_map.ContainsKey(k))
                throw new InvalidOperationException("Already bound " + id);
            var p = _factory.Create(bp);
            p.Bind(id, bp);
            _map[k] = p;
            return p;
        }

        public bool TryGet(EntityId id, out PresentActor p) =>
            _map.TryGetValue(HitboxComp.Pack(id), out p);

        public void AfterLogicTick(CombatWorld w)
        {
            Copy();
            for (int i = 0; i < _scratch.Count; i++)
                _scratch[i].SyncLogic(w);
        }

        public void LateUpdate(CombatWorld w, float dt, float renderTime)
        {
            Copy();
            for (int i = 0; i < _scratch.Count; i++)
            {
                if (_scratch[i].TryGet<PoseFollowPresent>(out var p))
                    p.SetRenderLogicTime(renderTime);
                _scratch[i].LateTick(dt);
            }
        }

        public void PumpUnscaled(float dt)
        {
            _cues.Pump(dt);
            _floaters.Pump(dt);
        }

        public void OnCleanup(EvEntityCleanup e)
        {
            long k = HitboxComp.Pack(e.Id);
            if (_map.TryGetValue(k, out var p))
            {
                _map.Remove(k);
                _factory.Release(p);
            }
        }

        public void NotifyLocalHitstop()
        {
            if (TryGet(_local, out var p) && p.TryGet<PlayerCameraPresent>(out var c))
                c.NotifyImpulse();
        }

        public bool TryGetLocalHud(out HudSnapshot s)
        {
            s = default(HudSnapshot);
            return TryGet(_local, out var p)
                && p.TryGet<PlayerHudSourcePresent>(out var h)
                && (s = h.Snapshot).Valid;
        }

        public bool TryGetLocalCamera(out PlayerCameraPresent c)
        {
            c = null;
            return TryGet(_local, out var p) && p.TryGet(out c);
        }

        public bool TryGetLocalFeedback(out PlayerFeedbackPresent feedback)
        {
            feedback = null;
            return TryGet(_local, out var actor) && actor.TryGet(out feedback);
        }

        public void ReleaseAll()
        {
            Copy();
            _map.Clear();
            for (int i = 0; i < _scratch.Count; i++)
                _factory.Release(_scratch[i]);
            _cues.ReturnAll();
            _floaters.ReturnAll();
        }

        void OnDamage(EvDamage e)
        {
            _floaters.OnDamage(e);
            if (
                e.Target == _local
                && TryGet(_local, out var p)
                && p.TryGet<PlayerFeedbackPresent>(out var f)
            )
                f.OnHurt(e);
        }

        void OnImmune(EvImmune e)
        {
            _floaters.OnImmune(e);
            if (
                e.Target == _local
                && TryGet(_local, out var p)
                && p.TryGet<PlayerFeedbackPresent>(out var f)
            )
                f.OnImmune(e);
        }

        void OnHeal(EvHeal e) => _floaters.OnHeal(e);

        void Copy()
        {
            _scratch.Clear();
            foreach (var p in _map)
                _scratch.Add(p.Value);
        }
    }
}
