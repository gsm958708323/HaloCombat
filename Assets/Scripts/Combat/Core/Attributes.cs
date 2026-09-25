using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>
    /// 属性槽位 id。底层 short 只是为了紧凑；数值即数组下标，AttributeSet 按下标取值，
    /// 所以不能重排、不能插空（SlotCount 与最后一项绑定）。
    /// </summary>
    public enum AttrId : short
    {
        /// <summary>当前生命值；唯一合法写入口是 SetBase(AttrId.Hp)，它会夹到 [0, MaxHp]。</summary>
        Hp = 0,
        /// <summary>生命上限；改动后当前血量会被主动收缩到新上限内。</summary>
        MaxHp = 1,
        /// <summary>护盾值；SetBase 会把负值夹到 0。</summary>
        Shield = 2,
        /// <summary>攻击力，伤害结算的基准值（BarrelExplosionEffect 按它算爆炸伤害）。</summary>
        Atk = 3,
        /// <summary>防御力。</summary>
        Def = 4,
        /// <summary>移动速度。</summary>
        MoveSpeed = 5,
        /// <summary>暴击率（[0,1] 语义）。</summary>
        CritRate = 6,
        /// <summary>造成伤害的乘区，默认 1，调高为增伤。</summary>
        DmgDealMul = 7,
        /// <summary>受到伤害的乘区，默认 1，调高为易伤。</summary>
        DmgTakenMul = 8,
        /// <summary>动作速度，默认 1；用于缩放技能/动画节奏。</summary>
        ActionSpeed = 9
    }

    /// <summary>
    /// 修改器运算：Add 先累加、Mul 后连乘，Override 一旦存在就直接取代前两者（见 AttributeSet.Recalc）。
    /// </summary>
    public enum ModOp : byte { Add = 0, Mul = 1, Override = 2 }

    /// <summary>
    /// 单条属性修改器：Attr/Op/Value 描述怎么改，SourceId 是撤销凭据（RemoveBySource），
    /// Priority 只在多个 Override 之间比大小。
    /// </summary>
    public struct Modifier
    {
        public AttrId Attr;
        public ModOp Op;
        public float Value;
        public int SourceId;    // 撤销凭据：RemoveBySource 按它批量移除
        public int Priority;    // 仅 Override 之间比较，大者胜
    }

    /// <summary>
    /// 属性集：Base（基值，走 SetBase）与 Modifier（修改器，走 AddMod/RemoveBySource）两层，GetFinal 才是对外读的最终值。
    /// 不变量：血量只能通过 SetBase(AttrId.Hp) 修改——它负责夹到 [0, MaxHp]；Recalc 对 Hp/Shield 直接返回基值、
    /// 不受修改器影响，所以绕过 SetBase 改血必然越界。
    /// </summary>
    public sealed class AttributeSet : Comp
    {
        const int SlotCount = 10;
        readonly float[] _base = new float[SlotCount];
        readonly float[] _final = new float[SlotCount];
        readonly bool[] _validFinal = new bool[SlotCount];
        readonly List<Modifier> _mods = new List<Modifier>(16);

        public int ModCount => _mods.Count;

        public AttributeSet()
        {
            _base[(int)AttrId.DmgDealMul] = 1f;
            _base[(int)AttrId.DmgTakenMul] = 1f;
            _base[(int)AttrId.ActionSpeed] = 1f;
        }

        /// <summary>
        /// 角色初始属性（战士默认值）。在组件装配完成后调用，作为每个新角色/复用角色的基线。
        /// </summary>
        public void InitFighterDefaults()
        {
            SetBase(AttrId.MaxHp, 100f);
            SetBase(AttrId.Hp, 100f);
            SetBase(AttrId.Shield, 0f);
            SetBase(AttrId.Atk, 10f);
            SetBase(AttrId.Def, 0f);
            SetBase(AttrId.MoveSpeed, 5f);
            SetBase(AttrId.CritRate, 0f);
            SetBase(AttrId.DmgDealMul, 1f);
            SetBase(AttrId.DmgTakenMul, 1f);
            SetBase(AttrId.ActionSpeed, 1f);
        }

        public float GetBase(AttrId id) => _base[(int)id];
        public bool IsAlive() => GetBase(AttrId.Hp) > 0f;

        /// <summary>
        /// 唯一的基值写入口，附带两条不变量：Hp 夹到 [0, MaxHp]、Shield 夹到非负；
        /// 改 MaxHp 后主动把当前血量向新上限收缩，避免出现 Hp &gt; MaxHp 的中间态。
        /// </summary>
        public void SetBase(AttrId id, float v)
        {
            if (id == AttrId.Hp)
            {
                if (v < 0f) v = 0f;
                float max = GetFinal(AttrId.MaxHp);
                if (max > 0f && v > max) v = max;
            }
            else if (id == AttrId.Shield && v < 0f)
            {
                v = 0f;
            }

            _base[(int)id] = v;
            Invalidate(id);
            if (id == AttrId.MaxHp)
                ClampHpToMax();
        }

        /// <summary>
        /// 取最终值：惰性缓存 + 脏标记，任何 base/修改器改动都会 Invalidate，下次读取时才重算。
        /// 因此不要每帧无脑遍历重算；失效是按 AttrId 粒度的，跨属性依赖由写入方显式处理。
        /// </summary>
        public float GetFinal(AttrId id)
        {
            int i = (int)id;
            if (_validFinal[i]) return _final[i];
            float value = Recalc(id);
            _final[i] = value;
            _validFinal[i] = true;
            return value;
        }

        /// <summary>
        /// 追加一个修改器并让该属性缓存失效；SourceId 是后续 RemoveBySource 的撤销凭据。
        /// </summary>
        public void AddMod(in Modifier m)
        {
            _mods.Add(m);
            Invalidate(m.Attr);
            if (m.Attr == AttrId.MaxHp)
                ClampHpToMax();
        }

        /// <summary>
        /// 撤销同一来源（如某个 buff）的全部修改器：倒序遍历避免移除时索引跳项，最后统一收缩血量。
        /// </summary>
        public void RemoveBySource(int sourceId)
        {
            bool any = false;
            for (int i = _mods.Count - 1; i >= 0; i--)
            {
                if (_mods[i].SourceId != sourceId) continue;
                Invalidate(_mods[i].Attr);
                _mods.RemoveAt(i);
                any = true;
            }

            if (any) ClampHpToMax();
        }

        /// <summary>
        /// 归还对象池时重置：清空基值/缓存/修改器，并把三个默认乘区重新置 1，
        /// 否则复用对象会继承上一个角色的加成。
        /// </summary>
        protected override void OnDetach()
        {
            Array.Clear(_base, 0, SlotCount);
            Array.Clear(_final, 0, SlotCount);
            Array.Clear(_validFinal, 0, SlotCount);
            _mods.Clear();
            _base[(int)AttrId.DmgDealMul] = 1f;
            _base[(int)AttrId.DmgTakenMul] = 1f;
            _base[(int)AttrId.ActionSpeed] = 1f;
        }

        /// <summary>
        /// 求值顺序固定为 Override &gt; (Base + Add) * Mul：只要存在 Override，Add/Mul 全部被忽略，
        /// 多个 Override 取 Priority 最大者（Priority 相等时后加入者胜）。顺序即契约，改动会改掉所有数值手感。
        /// </summary>
        float Recalc(AttrId id)
        {
            if (id == AttrId.Hp || id == AttrId.Shield)
                return _base[(int)id];

            float add = 0f;
            float mul = 1f;
            bool hasOv = false;
            int ovPri = int.MinValue;
            float ovVal = 0f;

            for (int i = 0; i < _mods.Count; i++)
            {
                var m = _mods[i];
                if (m.Attr != id) continue;
                if (m.Op == ModOp.Add) add += m.Value;
                else if (m.Op == ModOp.Mul) mul *= m.Value;
                else if (m.Op == ModOp.Override)
                {
                    if (!hasOv || m.Priority > ovPri || m.Priority == ovPri)
                    {
                        hasOv = true;
                        ovPri = m.Priority;
                        ovVal = m.Value;
                    }
                }
            }

            if (hasOv) return ovVal;
            return (_base[(int)id] + add) * mul;
        }

        void Invalidate(AttrId id) => _validFinal[(int)id] = false;

        void ClampHpToMax()
        {
            float max = GetFinal(AttrId.MaxHp);
            float hp = _base[(int)AttrId.Hp];
            if (max > 0f && hp > max)
            {
                _base[(int)AttrId.Hp] = max;
                Invalidate(AttrId.Hp);
            }
        }
    }
}
