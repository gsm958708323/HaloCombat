# ADR-0020 接触确认与真实Combo输入

日期：2026-11-15。状态：已实施。关联N02/N04/K03。验收见`docs/验证/2026-11-15-confirmed-combos.md`。

历史fae7872的ComboConfirm(1017)由当前有效施法接触敌人授予，在无敌/护盾/零伤害检查前取得；不是HP扣血事件。资格绑定完整来源EntityId、Skill与CastId，只在这次Timeline仍播放时有效，旧弹体不能替后来的施法取得资格。自然结束、打断、取消、死亡和替换均释放自己的TagLease，不清除外部同名Tag。

Combo仍为边表。ComboComp统一停止/活动/Cancel守卫，守卫不通过时不查表/不消费确认；查完整张表后无论匹配成功与否均释放自己的确认，SkillCast资源或目标守卫失败也不恢复确认，但输入仅成功后消费。Table.TryResolve保持无副作用，平级且前驱重叠的输入条目因正向Tag可能同时满足而在编译时拒绝。

原Hitbox必须捕获开启时的SkillOrigin并传给ApplyEffectsIntent，不能从延迟结算时的新导演状态猜归属。导演持有自己当前施法确认；结束/替换时先清确认再发布结束事件，避免完成回调和旧弹体延迟命中留下过期资格。命中事件与扣血后事件继续分开。

网页增加Combo边表编辑与真实输入入口；预览Caster仅在工程含runtime时安装InputBuffer/Combo/RuntimePlayerDriver，普通技能按钮仍走相同SkillCastComp。input命令只投递令牌，不绕过Combo或重置会话。网页/Unity SkillLab/正式场景使用同一表和运行组件；试验选择技能仍是编辑操作，会重建预览，连续连招应在同会话发送输入。

技能F6/F7发布闭包仍明确省略角色/Combo游戏调度，不能从一包技能猜回完整键表；完整正式JSON保留Combo。Unity SkillLab的SubmitInput只有实际装入的表能匹配，空表不会回退到代码；正式Arena从完整工程读表。完整运行内容包导出仍属L04独立任务。

本批先恢复通用确认/输入语义，追加ConfirmAttack示例边（G1→G2需1017），原六技能Attack表作为已迁移基线保留。历史Arena2001→2012→2013及其共享Timeline/Projectile身份与完整数值继续独立迁移，不能把G1/G2示例称为旧十三技能完成。

验收覆盖无敌/护盾/零伤害/友方/死亡目标、当前/旧施法/来源代际、全部结束路径、外部同名Lease、查表未命中/施放失败/Cancel外/停止时的消费差异、表优先级/冲突、真实Hitbox/弹体、网页输入/身份操作/保存以及MCP实际场景和跨宿主确定输入。重要节点通过后本地提交。
