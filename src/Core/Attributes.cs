using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum AttrId : short
    {
        Hp = 0,
        MaxHp = 1,
        Shield = 2,
        Atk = 3,
        Def = 4,
        MoveSpeed = 5,
        CritRate = 6,
        DmgDealMul = 7,
        DmgTakenMul = 8
    }

    public enum ModOp : byte { Add = 0, Mul = 1, Override = 2 }

    public struct Modifier
    {
        public AttrId Attr;
        public ModOp Op;
        public float Value;
        public int SourceId;
        public int Priority;
    }

    public sealed class AttributeSet : Comp
    {
        const int SlotCount = 9;
        readonly float[] _base = new float[SlotCount];
        readonly float[] _final = new float[SlotCount];
        readonly bool[] _validFinal = new bool[SlotCount];
        readonly List<Modifier> _mods = new List<Modifier>(16);

        public int ModCount => _mods.Count;

        public AttributeSet()
        {
            _base[(int)AttrId.DmgDealMul] = 1f;
            _base[(int)AttrId.DmgTakenMul] = 1f;
        }

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
        }

        public float GetBase(AttrId id) => _base[(int)id];
        public bool IsAlive() => GetBase(AttrId.Hp) > 0f;

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

        public float GetFinal(AttrId id)
        {
            int i = (int)id;
            if (_validFinal[i]) return _final[i];
            float value = Recalc(id);
            _final[i] = value;
            _validFinal[i] = true;
            return value;
        }

        public void AddMod(in Modifier m)
        {
            _mods.Add(m);
            Invalidate(m.Attr);
            if (m.Attr == AttrId.MaxHp)
                ClampHpToMax();
        }

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

        protected override void OnDetach()
        {
            Array.Clear(_base, 0, SlotCount);
            Array.Clear(_final, 0, SlotCount);
            Array.Clear(_validFinal, 0, SlotCount);
            _mods.Clear();
            _base[(int)AttrId.DmgDealMul] = 1f;
            _base[(int)AttrId.DmgTakenMul] = 1f;
        }

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
