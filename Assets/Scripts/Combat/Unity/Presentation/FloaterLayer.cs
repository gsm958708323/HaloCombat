using System;
using Combat.Core;

namespace Combat.Unity.Presentation
{
    public readonly struct FloaterRequest
    {
        public readonly EntityId Target;
        public readonly SimVec3 LogicPos;
        public readonly float Amount;
        public readonly bool Crit,
            Kill,
            Immune,
            Heal;
        public readonly float ShieldAbsorb;

        public FloaterRequest(
            EntityId t,
            SimVec3 p,
            float a,
            bool c,
            bool k,
            bool i,
            bool h,
            float s
        )
        {
            Target = t;
            LogicPos = p;
            Amount = a;
            Crit = c;
            Kill = k;
            Immune = i;
            Heal = h;
            ShieldAbsorb = s;
        }
    }

    public interface IFloaterPool
    {
        bool TryPlay(in FloaterRequest req);
        void TickUnscaled(float dt);
        void ReturnAll();
    }

    public sealed class NullFloaterPool : IFloaterPool
    {
        public bool TryPlay(in FloaterRequest r) => true;

        public void TickUnscaled(float d) { }

        public void ReturnAll() { }
    }

    public sealed class FloaterLayer
    {
        CombatWorld _world;
        IFloaterPool _pool = new NullFloaterPool();
        Func<EntityId, string, SimVec3?> _anchorResolver;

        public void SetWorld(CombatWorld w) => _world = w;

        public void SetAnchorResolver(Func<EntityId, string, SimVec3?> resolver)
            => _anchorResolver = resolver;

        public void SetPool(IFloaterPool p) => _pool = p ?? new NullFloaterPool();

        public void OnDamage(EvDamage e)
        {
            if (TryHead(e.Target, out var p))
                _pool.TryPlay(
                    new FloaterRequest(
                        e.Target,
                        p,
                        e.Amount,
                        e.IsCrit,
                        e.IsKill,
                        false,
                        false,
                        e.ShieldAbsorb
                    )
                );
        }

        public void OnImmune(EvImmune e)
        {
            if (TryHead(e.Target, out var p))
                _pool.TryPlay(new FloaterRequest(e.Target, p, 0f, false, false, true, false, 0f));
        }

        public void OnHeal(EvHeal e)
        {
            if (TryHead(e.Target, out var p))
                _pool.TryPlay(
                    new FloaterRequest(e.Target, p, e.Amount, false, false, false, true, 0f)
                );
        }

        public void Pump(float dt) => _pool.TickUnscaled(dt);

        public void ReturnAll() => _pool.ReturnAll();

        bool TryHead(EntityId id, out SimVec3 p)
        {
            // The anchor policy lives in Core.FloaterAnchor so it stays unit-testable.
            if (!TryBody(id, out var body))
            {
                p = default(SimVec3);
                return false;
            }
            p = FloaterAnchor.Resolve(body, _anchorResolver, id);
            return true;
        }

        bool TryBody(EntityId id, out SimVec3 p)
        {
            p = default(SimVec3);
            return _world != null
                && _world.TryGetActor(id, out var a)
                && a != null
                && a.TryGetComp<TransformComp>(out var tf)
                && (p = tf.Position).X == tf.Position.X;
        }
    }
}
