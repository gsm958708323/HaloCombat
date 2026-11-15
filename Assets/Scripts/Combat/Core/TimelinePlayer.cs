using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class TimelinePlayer
    {
        readonly List<LiveClip> _live = new List<LiveClip>(8);
        readonly List<ClipRuntime> _pendingClips = new List<ClipRuntime>();
        readonly List<PendingPayload> _pendingPayloads = new List<PendingPayload>();
        struct PendingPayload { public double Time; public IEffect[] Effects; public SkillAim Aim; public float Power; }
        bool[] _clipOpened = new bool[8];
        long[] _payloadCounts = new long[8];
        TimelineDefinition _so;
        double _time;
        bool _playing;
        bool _finishPending;
        SkillAim _aim;
        Func<float, float> _advanceBudget;
        Func<int, bool> _payPulse;
        Func<SkillAim?> _aimResolver;
        int _emitted;
        ChargeDefinition _charge;
        double _chargeTime;
        bool _charging;
        float _basePower=1;
        SkillOrigin _origin;
        public SkillAim LastAim=>_aim;

        public bool IsPlaying => _playing;
        public float Time => (float)_time;
        public TimelineId Id => _so != null ? _so.Id : TimelineId.None;
        public TimelineDefinition Definition=>_so;
        public DirectorStopReason EndReason { get; private set; }
        public bool IsCharging => _charging;
        public float ChargeProgress => _charge != null ? _charge.Progress((float)_chargeTime) : 0;
        public float Power { get; private set; } = 1;
        public float ChargePower => _charge != null ? _basePower*_charge.Power((float)_chargeTime) : Power;
        public event Action<float> Released;

        public void Play(TimelineDefinition so, SkillAim aim = default, Func<float, float> advanceBudget = null,
            Func<int, bool> payPulse = null, Func<SkillAim?> aimResolver = null, ChargeDefinition charge = null,float power=1,SkillOrigin origin=default)
        {
            StopInternal(true);
            _so = so ?? throw new ArgumentNullException(nameof(so));
            _aim = aim;
            _advanceBudget = advanceBudget; _payPulse = payPulse; _aimResolver = aimResolver;
            _emitted = 0; EndReason = DirectorStopReason.Finished;
            _charge = charge; _chargeTime = 0; _charging = charge != null;
            _basePower=power;Power=power;
            _origin=origin;
            _time = 0f;
            _playing = true;
            EnsureFlags(_so.Clips?.Length ?? 0, ref _clipOpened);
            int count = _so.Payloads?.Length ?? 0;
            if (_payloadCounts.Length < count) _payloadCounts = new long[count];
            Array.Clear(_payloadCounts, 0, _payloadCounts.Length);
        }

        public void Stop() => StopInternal(true);

        public bool ReleaseCharge()
        {
            if (!_charging) return false;
            if (_chargeTime + .000001 < _charge.MinimumTime)
            { EndReason = DirectorStopReason.EarlyRelease; StopInternal(true); return false; }
            if (_aimResolver != null)
            {
                var aim = _aimResolver();
                if (!aim.HasValue) { EndReason = DirectorStopReason.TargetLost; StopInternal(true); return false; }
                _aim = aim.Value; _aimResolver = null;
            }
            Power = _basePower*_charge.Power((float)_chargeTime);
            _charging = false;
            Released?.Invoke(Power);
            return true;
        }

        public void Tick(float dt, Actor self)
        {
            if (!_playing || _so == null) return;
            if (_charging)
            {
                float request = _charge.AutoRelease ? Math.Min(dt, Math.Max(0, _charge.MaximumTime - (float)_chargeTime)) : dt;
                float budget = _advanceBudget == null ? request : _advanceBudget(request);
                _chargeTime = Math.Min(_charge.MaximumTime, _chargeTime + budget);
                if (budget + .000001 < request)
                { EndReason = DirectorStopReason.EnergyDepleted; StopInternal(true); return; }
                if (!_charge.AutoRelease || _chargeTime + .000001 < _charge.MaximumTime) return;
                ReleaseCharge();
                if (!_playing) return;
                dt = Math.Max(0, dt - budget);
            }
            float requested = Math.Min(dt, Math.Max(0f, _so.Duration - (float)_time));
            float allowed = _advanceBudget == null ? requested : _advanceBudget(requested);
            double previous = _time;
            _time += allowed;
            FirePayloads(self);
            if (!_playing) return;
            OpenDue(self,previous);
            TickLive(previous);
            if (!_playing) return;
            if (allowed + .000001f < requested)
            { EndReason = DirectorStopReason.EnergyDepleted; _finishPending=true; }
            else if (_time + .000001 >= _so.Duration)
                _finishPending=true;
        }

        // Keep the final step's hitbox, movement policy and control leases alive until
        // the world has integrated motion and delivered its queued contact effects.
        public void FlushPendingCloses()
        {
            if(!_playing)return;
            CloseDue();
            if(_finishPending)StopInternal(EndReason!=DirectorStopReason.Finished);
        }

        void OpenDue(Actor self,double previous)
        {
            var clips = _so.Clips;
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
            {
                // A clip needs a nonempty overlap with this step. Merely reaching its
                // start cannot open a zero-time hitbox or grant an early iframe.
                if(clips[i].OnPayload||_clipOpened[i]||Math.Min(_time,clips[i].End)-Math.Max(previous,clips[i].Start)<=.0000001)continue;
                _clipOpened[i] = true;
                var rt = new ClipRuntime
                {
                    Self = self,
                    Clip = clips[i],
                    Start = clips[i].Start,
                    End = clips[i].End, Aim = _aim, Power = Power,Origin=_origin, Sequence = self.World.NextActionInstanceId()
                };
                var handler = ClipHandlerFactory.Create(clips[i].Kind);
                handler.Open(rt);
                _live.Add(new LiveClip { Handler = handler, Rt = rt });
            }
            for (int i = 0; i < _pendingClips.Count;)
            {
                var rt = _pendingClips[i];
                if(Math.Min(_time,rt.End)-Math.Max(previous,rt.Start)<=.0000001){i++;continue;}
                _pendingClips.RemoveAt(i);
                var handler = ClipHandlerFactory.Create(rt.Clip.Kind); handler.Open(rt);
                _live.Add(new LiveClip { Handler = handler, Rt = rt });
            }
        }

        void TickLive(double previous)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                var live = _live[i];
                float overlap = (float)Math.Max(0, Math.Min(_time, live.Rt.End) - Math.Max(previous, live.Rt.Start));
                live.Handler.Tick(live.Rt, overlap);
            }
        }

        void CloseDue()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var live = _live[i];
                if (_time + .000001 < live.Rt.End) continue;
                live.Handler.Close(live.Rt, false);
                _live.RemoveAt(i);
            }
        }

        void FirePayloads(Actor self)
        {
            var payloads = _so.Payloads;
            if (payloads == null || self.World == null) return;
            while (_playing)
            {
                int index = -1; double first = double.PositiveInfinity;
                for (int i = 0; i < payloads.Length; i++)
                {
                    var payload = payloads[i];
                    if (payload.OnPayload) continue;
                    if (_payloadCounts[i] > 0 && payload.RepeatInterval <= 0) continue;
                    double next = payload.Time + _payloadCounts[i] * (double)payload.RepeatInterval;
                    if (payload.RepeatInterval > 0 && next >= _so.Duration - .000001) continue;
                    if (next > _time + .000001 || next >= first) continue;
                    first = next; index = i;
                }
                int pendingIndex = -1;
                for (int i = 0; i < _pendingPayloads.Count; i++)
                    if (_pendingPayloads[i].Time <= _time + .000001 && _pendingPayloads[i].Time <= first)
                    { if (pendingIndex < 0 || _pendingPayloads[i].Time < _pendingPayloads[pendingIndex].Time) pendingIndex = i; }
                if (pendingIndex >= 0)
                {
                    var pending = _pendingPayloads[pendingIndex]; _pendingPayloads.RemoveAt(pendingIndex);
                    Deliver(self, pending.Effects, pending.Aim, pending.Power,_origin); continue;
                }
                if (index < 0) return;
                if (_aimResolver != null)
                {
                    var aim = _aimResolver();
                    if (!aim.HasValue) { EndReason = DirectorStopReason.TargetLost; StopInternal(true); return; }
                    _aim = aim.Value;
                }
                if (_payPulse != null && !_payPulse(_emitted))
                { EndReason = DirectorStopReason.EnergyDepleted; StopInternal(true); return; }
                _emitted++; _payloadCounts[index]++;
                QueueActions(self, index, first);
                Deliver(self, payloads[index].Effects, _aim, Power,_origin);
            }
        }

        void QueueActions(Actor self, int index, double triggerTime)
        {
            foreach (var clip in _so.Clips ?? Array.Empty<TimelineClip>())
            {
                if (!clip.OnPayload || clip.PayloadIndex != index) continue;
                float start = (float)triggerTime + clip.Start;
                float end = Math.Min(_so.Duration, (float)triggerTime + clip.End);
                if (start >= end) continue;
                _pendingClips.Add(new ClipRuntime { Self = self, Clip = clip, Start = start, End = end, Aim = _aim, Power = Power, Origin=_origin,Sequence = self.World.NextActionInstanceId() });
            }
            foreach (var payload in _so.Payloads)
                if (payload.OnPayload && payload.PayloadIndex == index && triggerTime + payload.Time < _so.Duration)
                    _pendingPayloads.Add(new PendingPayload { Time = triggerTime + payload.Time, Effects = payload.Effects, Aim = _aim, Power = Power });
        }

        static void Deliver(Actor self, IEffect[] effects, SkillAim aim, float power,SkillOrigin origin)
        {
            float atk = self.TryGetComp<AttributeSet>(out var attr) ? attr.GetFinal(AttrId.Atk) : 0;
            Actor target = null;
            if (aim.Target.IsValid && self.World.TryGetActor(aim.Target, out var selected)) target = selected;
            else if (!aim.HasPoint && self.TryGetComp<BehaviorTreeComp>(out var bt)) self.World.TryGetActor(bt.Board.Target, out target);
            self.World.Deliver(effects, self, target, atk, aim.HasPoint ? aim.Point : (SimVec3?)null,
                aim.HasPoint ? LocomotionComp.ForwardFromYaw(aim.Yaw) : (SimVec3?)null, power: power,
                origin:origin);
        }

        void StopInternal(bool interrupted)
        {
            for (int i = 0; i < _live.Count; i++)
                _live[i].Handler.Close(_live[i].Rt, interrupted);
            _live.Clear();
            _pendingClips.Clear(); _pendingPayloads.Clear();
            _playing = false;
            _finishPending=false;
            _so = null;
            _time = 0f;
            _charging = false; _charge = null; _chargeTime = 0;
            _advanceBudget = null; _payPulse = null; _aimResolver = null;
        }

        static void EnsureFlags(int n, ref bool[] flags)
        {
            if (flags.Length < n) flags = new bool[n];
            for (int i = 0; i < flags.Length; i++) flags[i] = false;
        }
    }
}
