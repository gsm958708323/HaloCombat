namespace Combat.Core
{
    public sealed class BuffAttrBridgeComp : Comp
    {
        BuffComp _buffs;
        AttrComp _attr;
        readonly System.Collections.Generic.Dictionary<int, float> _atkFlatByBuff =
            new System.Collections.Generic.Dictionary<int, float>();

        // Demo：预注册 BuffType → 攻击 flat
        public void RegisterAtkFlat(BuffTypeId type, float flat)
            => _atkFlatByBuff[type.Value] = flat;

        public override bool WantsTick => true;

        protected override void OnAttach()
        {
            _buffs = Self.GetComp<BuffComp>();
            _attr = Self.GetComp<AttrComp>();
            RegisterAtkFlat(new BuffTypeId(9001), 7f);
        }

        public override void Tick(float dt)
        {
            // 简法：每帧按「是否拥有 Buff」重写 flat（表很小，Demo 可接受）
            foreach (var kv in _atkFlatByBuff)
            {
                var id = new BuffTypeId(kv.Key);
                if (_buffs.AllBuffs.ContainsKey(id))
                    _attr.AddFlat(id, kv.Value);
                else
                    _attr.AddFlat(id, 0f); // 或提供 RemoveFlat API
            }
        }

        // public void RemoveModifier(BuffTypeId type)
        // {
        //     _flat.Remove(type);
        //     _percent.Remove(type);
        // }
    }
}
