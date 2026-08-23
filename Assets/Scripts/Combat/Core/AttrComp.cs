using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>
    /// 属性读取统一走这里，供 DamageFormula 使用。
    /// Total* 已含 AttrComp 的 flat/percent 登记。
    /// </summary>
    public static class AttrQuery
    {
        public static float Atk(AttrComp a) => a != null ? a.TotalAtk : 0f;
        public static float Def(AttrComp a) => a != null ? a.TotalDef : 0f;
        public static float MaxHp(AttrComp a) => a != null ? a.TotalMaxHp : 1f;
    }
    
    public sealed class AttrComp : Comp
    {
        readonly Dictionary<BuffTypeId, float> _flat = new Dictionary<BuffTypeId, float>();
        readonly Dictionary<BuffTypeId, float> _percent = new Dictionary<BuffTypeId, float>();

        public float BaseAtk { get; set; } = 10f;
        public float BaseDef { get; set; } = 0f;
        public float BaseMaxHp { get; set; } = 100f;

        public float DamageTakenMul = 1f; // 易伤/减伤总乘区（MVP 一个口）
        public float DamageDealMul = 1f;

        public void Setup(float atk, float def, float maxHp)
        {
            BaseAtk = atk;
            BaseDef = def;
            BaseMaxHp = maxHp > 1f ? maxHp : 1f;
        }

        public void AddFlat(BuffTypeId type, float value) => _flat[type] = value;
        public void AddPercent(BuffTypeId type, float percent) => _percent[type] = percent;

        public float TotalAtk => BaseAtk + Sum(_flat) + Sum(_percent) * BaseAtk;
        public float TotalDef => BaseDef + Sum(_flat) + Sum(_percent) * BaseDef;
        public float TotalMaxHp => BaseMaxHp + Sum(_flat) + Sum(_percent) * BaseMaxHp;

        float Sum(Dictionary<BuffTypeId, float> dict)
        {
            float sum = 0f;
            foreach (var v in dict.Values)
                sum += v;
            return sum;
        }

        protected override void OnDetach()
        {
            _flat.Clear();
            _percent.Clear();
        }
    }
}
