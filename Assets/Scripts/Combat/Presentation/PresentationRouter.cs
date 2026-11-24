using Combat.Core;

namespace Combat.Presentation
{
    public sealed class PresentationRouter
    {
        readonly CombatWorld _world;
        readonly ViewRegistry _views;
        readonly ICuePlayer _cues;

        public PresentationRouter(CombatWorld world, ViewRegistry views, ICuePlayer cues = null)
        {
            _world = world;
            _views = views;
            _cues = cues;
        }

        public void Bind(EventBus bus)
        {
            if (bus == null || _views == null) return;
            bus.Subscribe<EvEntitySpawn>(_views.HandleSpawn);
            bus.Subscribe<EvEntityDead>(_views.HandleDead);
            bus.Subscribe<EvEntityCleanup>(_views.HandleCleanup);
            bus.Subscribe<EvHurt>(_views.HandleHurt);
            if (_cues != null) bus.Subscribe<EvCue>(_cues.Play);
        }

        public void Unbind(EventBus bus)
        {
            if (bus == null || _views == null) return;
            bus.Unsubscribe<EvEntitySpawn>(_views.HandleSpawn);
            bus.Unsubscribe<EvEntityDead>(_views.HandleDead);
            bus.Unsubscribe<EvEntityCleanup>(_views.HandleCleanup);
            bus.Unsubscribe<EvHurt>(_views.HandleHurt);
            if (_cues != null) bus.Unsubscribe<EvCue>(_cues.Play);
        }

        public void SampleLate(float alpha,bool paused=false)
        {
            bool hitstop = _world != null && _world.InHitstop;
            _views?.SampleAll(_world, hitstop ? 1f : alpha, hitstop,paused);
        }
    }
}
