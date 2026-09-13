using System.Collections.Generic;
using Combat.Core;

namespace Combat.Game
{
    public sealed class WinWatcher
    {
        readonly HashSet<long> _guards = new HashSet<long>();
        EntityId _player;
        public bool PlayerDead { get; private set; }
        public int GuardsLeft => _guards.Count;
        public bool Settled => PlayerDead || _guards.Count == 0;
        public bool PlayerWin => !PlayerDead && _guards.Count == 0;

        public void SetPlayer(EntityId id) => _player = id;

        public void RegisterGuard(EntityId id)
        {
            if (id.IsValid)
                _guards.Add(HitboxComp.Pack(id));
        }

        public void OnDead(EvEntityDead e)
        {
            _guards.Remove(HitboxComp.Pack(e.Id));
            if (e.Id == _player)
                PlayerDead = true;
        }
    }
}
