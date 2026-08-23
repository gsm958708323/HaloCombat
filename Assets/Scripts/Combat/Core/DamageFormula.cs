using System;

namespace Combat.Core
{
    public static class DamageFormula
    {
        /// <summary>
        /// MVP 公式：max(0, atk * power + flat - def) * dealMul * takenMul
        /// 商用可换成查表/脚本，但不改 DamageService 帧序。
        /// </summary>
        public static float Compute(
            AttrComp attacker,
            AttrComp defender,
            AttackSpec spec)
        {
            float atk = AttrQuery.Atk(attacker);
            float def = AttrQuery.Def(defender);
            float deal = attacker != null ? attacker.DamageDealMul : 1f;
            float taken = defender != null ? defender.DamageTakenMul : 1f;

            float raw = atk * spec.Power + spec.FlatBonus;
            if (!spec.IgnoreDef)
                raw -= def;

            if (raw < 0f)
                raw = 0f;

            return raw * deal * taken;
        }
    }
}
