using Combat.Core;

namespace Combat.Presentation
{
    public sealed class PresentationRouter
    {
        readonly CombatWorld _world;
        readonly ViewRegistry _views;
        readonly ICuePlayer _cues;
        readonly IFloaterPlayer _floaters;
        readonly IHitstopOverlay _hitstop;
        readonly IDebugOverlay _debug;

        public PresentationRouter(
            CombatWorld world,
            ViewRegistry views,
            ICuePlayer cues = null,
            IFloaterPlayer floaters = null,
            IHitstopOverlay hitstop = null,
            IDebugOverlay debug = null)
        {
            _world = world;
            _views = views;
            _cues = cues;
            _floaters = floaters;
            _hitstop = hitstop;
            _debug = debug;
        }

        public void Bind(EventBus bus)
        {
            if (bus == null || _views == null) return;
            bus.Subscribe<EvEntitySpawn>(_views.HandleSpawn);
            bus.Subscribe<EvEntityDead>(_views.HandleDead);
            bus.Subscribe<EvEntityCleanup>(_views.HandleCleanup);
            if (_cues != null) bus.Subscribe<EvCue>(_cues.Play);
            if (_floaters != null)
            {
                bus.Subscribe<EvDamage>(_floaters.Play);
                bus.Subscribe<EvImmune>(_floaters.PlayImmune);
            }

            if (_hitstop != null)
                bus.Subscribe<EvHitstop>(_ => _hitstop.ShowFlash());
        }

        public void Unbind(EventBus bus)
        {
            if (bus == null || _views == null) return;
            bus.Unsubscribe<EvEntitySpawn>(_views.HandleSpawn);
            bus.Unsubscribe<EvEntityDead>(_views.HandleDead);
            bus.Unsubscribe<EvEntityCleanup>(_views.HandleCleanup);
            if (_cues != null) bus.Unsubscribe<EvCue>(_cues.Play);
            if (_floaters != null)
            {
                bus.Unsubscribe<EvDamage>(_floaters.Play);
                bus.Unsubscribe<EvImmune>(_floaters.PlayImmune);
            }
        }

        public void SampleLate(float alpha)
        {
            bool hitstop = _world != null && _world.InHitstop;
            _views?.SampleAll(_world, hitstop ? 1f : alpha, hitstop);
            _hitstop?.SetActive(hitstop);
            if (_debug != null && _debug.Enabled)
                _debug.Refresh(_world);
        }
    }
}
