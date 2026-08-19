using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class TimelinePlayer
    {
        readonly EffectFactory _factory;
        readonly List<Effect> _active = new List<Effect>(8);
        readonly bool[] _fired;

        TimelineSO _so;
        float _time;
        bool _playing;

        public bool IsPlaying => _playing;
        public float Time => _time;
        public TimelineId Id => _so != null ? _so.Id : TimelineId.None;

        public TimelinePlayer(EffectFactory factory)
        {
            _factory = factory;
            _fired = null;
        }

        // 避免每 Play 都 alloc：用可扩容 fired 标记
        bool[] _firedKeys = new bool[16];

        public void Play(TimelineSO so)
        {
            StopInternal(clearEffects: true);
            _so = so ?? throw new System.ArgumentNullException(nameof(so));
            _time = 0f;
            _playing = true;

            int n = _so.Keys != null ? _so.Keys.Length : 0;
            if (_firedKeys.Length < n)
                _firedKeys = new bool[n];
            for (int i = 0; i < _firedKeys.Length; i++)
                _firedKeys[i] = false;
        }

        public void Stop()
        {
            StopInternal(clearEffects: true);
        }

        public void Tick(float dt, Actor self, TagComp tags)
        {
            if (!_playing || _so == null)
                return;

            float prev = _time;
            _time += dt;

            var keys = _so.Keys;
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    if (_firedKeys[i])
                        continue;

                    var key = keys[i];
                    // 跨越键点：prev < t <= time
                    if (key.Time > prev && key.Time <= _time)
                    {
                        var fx = _factory.Create(key, self, tags);
                        fx.Enter();
                        if (!fx.IsFinished)
                            _active.Add(fx);
                        if (key.FireOnce)
                            _firedKeys[i] = true;
                    }
                }
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var fx = _active[i];
                fx.Tick(dt);
                if (fx.IsFinished)
                {
                    fx.Exit();
                    _active.RemoveAt(i);
                }
            }

            if (_time >= _so.Duration)
                StopInternal(clearEffects: true);
        }

        void StopInternal(bool clearEffects)
        {
            if (clearEffects)
            {
                for (int i = 0; i < _active.Count; i++)
                {
                    _active[i].MarkFinished();
                    _active[i].Exit();
                }
                _active.Clear();
            }

            _playing = false;
            _so = null;
            _time = 0f;
        }
    }
}
