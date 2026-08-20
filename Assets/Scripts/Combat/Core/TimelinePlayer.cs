using System.Collections.Generic;

namespace Combat.Core
{
    struct LiveKeyEffect
    {
        public int KeyIndex;
        public float EndTime;   // 瞬时用 -1
        public Effect Fx;
        public bool Interval;
    }

    public sealed class TimelinePlayer
    {
        readonly EffectFactory _factory;
        readonly List<LiveKeyEffect> _live = new List<LiveKeyEffect>(8);
        bool[] _entered;
        TimelineSO _so;
        float _time;
        bool _playing;
        public bool IsPlaying => _playing;
        public float Time => _time;
        public TimelineId Id => _so != null ? _so.Id : TimelineId.None;
        public TimelinePlayer(EffectFactory factory)
        {
            _factory = factory;
            _entered = new bool[16];
        }
        public void Play(TimelineSO so)
        {
            StopInternal();
            _so = so;
            _time = 0f;
            _playing = true;
            int n = so.Keys?.Length ?? 0;
            if (_entered.Length < n)
                _entered = new bool[n];
            for (int i = 0; i < _entered.Length; i++)
                _entered[i] = false;
        }
        public void Stop() => StopInternal();
        public void Tick(float dt, Actor self)
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
                    if (_entered[i])
                        continue;
                    ref readonly var key = ref keys[i];
                    if (key.Time > prev && key.Time <= _time)
                    {
                        var fx = _factory.Create(key, self);
                        fx.Enter();
                        _entered[i] = true;
                        if (!fx.IsFinished)
                        {
                            _live.Add(new LiveKeyEffect
                            {
                                KeyIndex = i,
                                EndTime = key.IsInterval ? key.EndTime : -1f,
                                Fx = fx,
                                Interval = key.IsInterval
                            });
                        }
                        else
                        {
                            fx.Exit();
                        }
                    }
                }
            }
            // 推进区间 Effect + 到点 Exit
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var live = _live[i];
                var fx = live.Fx;
                if (live.Interval)
                {
                    if (_time >= live.EndTime)
                    {
                        // 补最后一小段：用不超过 End 的 dt 也可；MVP 直接 Exit
                        fx.Exit();
                        fx.MarkFinished();
                        _live.RemoveAt(i);
                        continue;
                    }
                    fx.Tick(dt);
                }
                else
                {
                    fx.Tick(dt);
                    if (fx.IsFinished)
                    {
                        fx.Exit();
                        _live.RemoveAt(i);
                    }
                }
            }
            if (_time >= _so.Duration)
                StopInternal();
        }
        void StopInternal()
        {
            for (int i = 0; i < _live.Count; i++)
            {
                var fx = _live[i].Fx;
                if (!fx.IsFinished)
                {
                    fx.Exit(); // 换轴/受击打断：区间 Tag 会 Remove
                    fx.MarkFinished();
                }
            }
            _live.Clear();
            _so = null;
            _time = 0f;
            _playing = false;
        }
    }
}
