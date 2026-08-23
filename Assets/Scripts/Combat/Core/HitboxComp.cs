namespace Combat.Core
{
    /// <summary>角色近战攻击盒（相对自身前向的胶囊/球近似）。</summary>
    public sealed class HitboxComp : Comp
    {
        public bool Active { get; private set; }
        public float Radius = 0.6f;
        public float OffsetForward = 0.8f;
        public float OffsetY = 1.0f;
        public int AttackSpecValue = 42;
        public int SourceSkillValue;

        readonly System.Collections.Generic.HashSet<long> _already =
            new System.Collections.Generic.HashSet<long>();

        public void Open(int attackSpecValue, int skillValue)
        {
            Active = true;
            AttackSpecValue = attackSpecValue;
            SourceSkillValue = skillValue;
            _already.Clear();
        }

        public void Close()
        {
            Active = false;
            _already.Clear();
        }

        public bool HasHit(EntityId id) => _already.Contains(Pack(id));
        public void Record(EntityId id) => _already.Add(Pack(id));

        protected override void OnDetach() => Close();

        static long Pack(EntityId id)
            => ((long)id.Index << 32) ^ (uint)id.Generation;

        public SimVec3 WorldCenter(TransformComp tf)
        {
            var f = tf.ForwardXZ();
            return new SimVec3(
                tf.Position.X + f.X * OffsetForward,
                tf.Position.Y + OffsetY,
                tf.Position.Z + f.Z * OffsetForward);
        }
    }
}
