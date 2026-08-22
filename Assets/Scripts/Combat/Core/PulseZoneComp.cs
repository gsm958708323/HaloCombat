using System;

namespace Combat.Core
{
    /// <summary>
    /// 场地脉冲：按 Interval Post AoEIntent；寿命到点 Despawn。
    /// </summary>
    public sealed class PulseZoneComp : Comp
    {
        readonly IntentQueue _intents;

        TransformComp _tf;
        EntityId _owner;
        int _ownerTeam;
        float _radius;
        float _interval;
        float _life;
        int _attackSpec;
        int _skillValue;
        float _acc;
        bool _armed;

        public override bool WantsTick => true;

        public PulseZoneComp(IntentQueue intents)
        {
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
        }

        public void Setup(
            EntityId owner,
            int ownerTeam,
            float radius,
            float interval,
            float lifetime,
            int attackSpecValue,
            int sourceSkillValue)
        {
            _owner = owner;
            _ownerTeam = ownerTeam;
            _radius = radius > 0f ? radius : 0.1f;
            _interval = interval > 0.05f ? interval : 0.05f;
            _life = lifetime > 0f ? lifetime : 0.1f;
            _attackSpec = attackSpecValue;
            _skillValue = sourceSkillValue;
            _acc = 0f;
            _armed = true;

            // 生成当帧立刻跳一次（可改成先等一个 interval）
            PulseOnce();
        }

        protected override void OnAttach()
        {
            _tf = Self.GetComp<TransformComp>();
        }

        protected override void OnDetach()
        {
            _armed = false;
            _tf = null;
        }

        public override void Tick(float dt)
        {
            if (!_armed) return;

            _life -= dt;
            if (_life <= 0f)
            {
                _armed = false;
                _intents.Post(new DespawnEntityIntent(Self.Id));
                return;
            }

            _acc += dt;
            while (_acc >= _interval)
            {
                _acc -= _interval;
                PulseOnce();
            }
        }

        void PulseOnce()
        {
            if (_tf == null) return;

            _intents.Post(new AoEIntent(
                source: Self.Id,
                owner: _owner,
                ownerTeam: _ownerTeam,
                shape: AoEShapeType.Circle,
                _tf.Position.X, _tf.Position.Y, _tf.Position.Z,
                _radius,
                _attackSpec,
                _skillValue,
                hitOwner: false));
        }
    }
}
