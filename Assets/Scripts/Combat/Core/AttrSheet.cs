using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>
    /// AttrSheet：唯一运行时属性真相源。
    /// BuffDefSO 描述数值；BuffComp 变更时触发脏标记；DamageService 只读 EnsureBuilt 后的快照。
    /// </summary>
    public sealed class AttrSheet : Comp
    {
        public BaseStats Base { get; set; } = new BaseStats(15f, 1f, 100f); // 默认值

        public AttrSnapshot Current { get; private set; } = new AttrSnapshot(15f, 1f, 100f);

        int _revision; // 与 BuffComp 同步
        bool _dirty;

        public void MarkDirty() => _dirty = true;
        public void MarkClean() => _dirty = false;

        public override bool WantsTick => true; // 仅脏算时跑

        public void EnsureBuilt()
        {
            if (!_dirty) return; // 简化（实际商用可在 BuffComp 变更时通知）
            Rebuild();
        }

        void Rebuild()
        {
            var snapshot = new AttrSnapshot(Base);
            foreach (var def in GetBuffDefs())
            {
                if (def.StackPolicy == BuffStackPolicy.RefreshDuration) continue;
                foreach (var mod in def.Modifiers)
                {
                    ApplyModifier(ref snapshot, mod, def.StackPolicy);
                }
            }
            Current = snapshot;
            _dirty = false;
            _revision++;
        }

        void ApplyModifier(ref AttrSnapshot sheet, AttrModifierDef mod, BuffStackPolicy policy)
        {
            switch (mod.Op)
            {
                case ModifierOp.Flat:
                    switch (mod.Channel)
                    {
                        case AttrChannel.Atk: sheet.Atk += mod.Value; break;
                        case AttrChannel.Def: sheet.Def += mod.Value; break;
                        case AttrChannel.MaxHp: sheet.MaxHp += mod.Value; break;
                        case AttrChannel.DamageDealMul: sheet.DamageDealMul += mod.Value; break;
                        case AttrChannel.DamageTakenMul: sheet.DamageTakenMul += mod.Value; break;
                    }
                    break;
                case ModifierOp.PercentBase:
                    switch (mod.Channel)
                    {
                        case AttrChannel.Atk: sheet.Atk += Base.Atk * mod.Value; break;
                            // 类似 Def, MaxHp
                    }
                    break;
            }
        }

        List<BuffDefSO> GetBuffDefs()
        {
            // 简化 Demo：硬编码或通过 BuffLookup 读
            // 商用：BuffLookup.TryGet 或 BuffComp 外部暴露
            return new List<BuffDefSO>();
        }

        public int Revision => _revision;
    }

    public struct AttrSnapshot
    {
        public float Atk;
        public float Def;
        public float MaxHp;
        public float DamageDealMul;
        public float DamageTakenMul;

        public AttrSnapshot(float atk, float def, float maxHp)
        {
            Atk = atk; Def = def; MaxHp = maxHp;
            DamageDealMul = 1f;
            DamageTakenMul = 1f;
        }

        public AttrSnapshot(BaseStats baseStats)
        {
            Atk = baseStats.Atk;
            Def = baseStats.Def;
            MaxHp = baseStats.MaxHp;
            DamageDealMul = 1f;
            DamageTakenMul = 1f;
        }
    }

    public struct BaseStats
    {
        public float Atk;
        public float Def;
        public float MaxHp;

        public BaseStats(float atk, float def, float maxHp)
        {
            Atk = atk; Def = def; MaxHp = maxHp;
        }
    }

    public sealed class BuffDefLookup
    {
        readonly Dictionary<BuffTypeId, BuffDefSO> _map = new();
        public bool TryGet(BuffTypeId id, out BuffDefSO def) => _map.TryGetValue(id, out def);
        public void Register(BuffDefSO def) => _map[def.Id] = def;
    }
}
