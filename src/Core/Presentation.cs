using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public struct CueDef
    {
        public int CueId;
        public string PrefabKey;
        public string SfxKey;
        public float LifeTime;
    }

    public sealed class CueLibrary
    {
        readonly Dictionary<int, CueDef> _map = new Dictionary<int, CueDef>(16);

        public void Register(in CueDef def)
        {
            if (def.CueId == 0) throw new ArgumentException("CueId");
            _map[def.CueId] = def;
        }

        public bool TryGet(int cueId, out CueDef def) => _map.TryGetValue(cueId, out def);

        public static CueLibrary DefaultCombat()
        {
            var lib = new CueLibrary();
            lib.Register(new CueDef { CueId = 101, PrefabKey = "fx_g1_slash", SfxKey = "sfx_slash", LifeTime = 0.4f });
            lib.Register(new CueDef { CueId = 102, PrefabKey = "fx_g2", SfxKey = "sfx_g2", LifeTime = 0.3f });
            lib.Register(new CueDef { CueId = CombatIds.CueFireballHit, PrefabKey = "fx_fireball_hit", SfxKey = "sfx_burn", LifeTime = 0.5f });
            lib.Register(new CueDef { CueId = CombatIds.CueFireGround, PrefabKey = "fx_fire_ground", SfxKey = "sfx_ground", LifeTime = 2f });
            return lib;
        }
    }

    public sealed class CueListener
    {
        readonly CueLibrary _lib;
        readonly List<EvCue> _played = new List<EvCue>(16);
        public IReadOnlyList<EvCue> Played => _played;
        public int Count => _played.Count;
        public CueListener(CueLibrary lib) => _lib = lib ?? throw new ArgumentNullException(nameof(lib));
        public void Bind(EventBus bus) => bus.Subscribe<EvCue>(OnCue);
        public void Unbind(EventBus bus) => bus.Unsubscribe<EvCue>(OnCue);
        public void Clear() => _played.Clear();

        void OnCue(EvCue e)
        {
            _lib.TryGet(e.CueId, out _);
            _played.Add(e);
        }

        public int CountId(int cueId)
        {
            int n = 0;
            for (int i = 0; i < _played.Count; i++)
                if (_played[i].CueId == cueId) n++;
            return n;
        }
    }
}
