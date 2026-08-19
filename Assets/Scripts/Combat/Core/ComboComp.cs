using System;

namespace Combat.Core
{
   public sealed class ComboComp : Comp
    {
        readonly ComboTableSO _table;
        InputBufferComp _input;
        TagComp _tags;
        SkillDirectorComp _director;
        public ComboComp(ComboTableSO table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }
        protected override void OnAttach()
        {
            _input = Self.GetComp<InputBufferComp>();
            _tags = Self.GetComp<TagComp>();
            _director = Self.GetComp<SkillDirectorComp>();
        }
        protected override void OnDetach()
        {
            _input = null;
            _tags = null;
            _director = null;
        }
        /// <summary>
        /// 用当前 Director 技能节点作 preSkill；匹配成功才 Consume。
        /// </summary>
        public bool TryResolve(out ComboResolveResult result)
        {
            result = default;
            if (_input == null || !_input.TryPeek(out var token))
                return false;
            var currentSkill = _director != null ? _director.CurrentSkill : SkillNodeId.None;
            if (!_table.TryResolve(currentSkill, token, _tags, out result))
                return false;
            _input.Consume();
            return true;
        }
    }
}
