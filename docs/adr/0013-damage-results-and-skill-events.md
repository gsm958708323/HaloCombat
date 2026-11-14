# ADR-0013：伤害结果、结算组件与技能事件

日期：2026-11-14。状态：本批已实施并验收，见[验证记录](../验证/2026-11-14-damage-skill-events.md)。对应H01-H04/H07-H10；其他效果/旧内容迁移仍按完整清单推进。

## 伤害的唯一执行路径

保留World.Deliver → EffectPipeline → DamageEffect。DamageEffect拆为独立文件，形成不可变DamageResult，明确计算伤害与实际资源变化；禁止通过日志反猜事件条件。

物理伤害减Def，通用元素伤害不减物理Def；元素目前是通用类别，不引入未配置的逐元素抗性。IgnoreDef仍是明确开关。计入加成=true时使用Atk*Coeff+Flat及来源DmgDealMul，false时只用Flat且来源加成置1；Power、目标Def、暴击和目标DmgTakenMul照常执行。UseSnapshotAtk只选择攻击取值时机，与计入加成独立。

顺序：Base=(IncludeBonuses?Atk*Coeff:0)+Flat；Scaled=Base*Power；AfterDefense=max(0,Scaled-有效Def)，需要Buff层数时再乘层数；Crit后乘来源和目标倍率；Final为护盾前最终值。Absorbed=min(Shield,Final)，HpLost=min(旧HP,max(0,Final-Absorbed))，Overkill=max(0,Final-Absorbed-HpLost)。EvDamage.Amount=Absorbed+HpLost，统计实际生效量，过量单独列出。死亡资格只在HP>0→HP=0转变一次；已死亡/失活目标不再重复扣血或击杀。无敌保留Immune事件，不触发伤害事件。

每次有效结算记录全世界唯一递增HitIndex、完整Source/Target身份、原始SkillOrigin、类型/基础/减防/暴击/倍率/最终/护盾/扣血/过量。结果只读，后续效果不能改写。治疗也只报告实际恢复量，死亡目标不复活。

AfterDamageEffect/after-damage节点仅在本调用的上一个DamageResult实际HpLost>0且Source/Target完整身份吻合时执行apply；可要求Origin与来源最近成功施法一致。无敌、0伤害、纯护盾、旧结果移给其他目标不能获得资格；next仍按显式图顺序执行一次。

## 技能事件

每个技能可有一个skill-events作者根，提供hit/critical/kill/completed/wall/range/projectile-end。hit定义为实际HpLost>0；critical还需本次暴击，kill只认HP首次归零。物理接触仍由投射物hit等既有端口表达，不将护盾命中冒充扣血确认。

Combat.Config将事件图编译进SkillEventCatalog，World按捕获的SkillOrigin分发；运行中不查询作者图。瞬发、连发、引导、蓄力一次成功Play对应一个CastId；完成只在Timeline自然Finished时触发一次，取消/打断/松开不冒充自然完成。完成使用最后瞄准、倍率与实例Origin，角色先退回可用状态再投递回调。

投射物原因事件在命中之后、对应终结包之后执行；通用projectile-end在通用结束包之后执行一次。原始施法可早于弹体命中结束，新施法不得改变旧技能事件归属。伤害事件回调按hit→critical→kill顺序，携带本次只读DamageResult/实际目标与点/方向。

事件产生的追加效果不再次触发该技能事件总线；不可变SkillOrigin携带SuppressedEvents标志，并被Buff/AoE/弹体等延迟实例捕获，避免下一帧重新绕回事件根。正常作者图即时环仍拒绝；通用深度/单逻辑步事件上限保留真实拒绝记录。这个规则不禁止追加伤害，只阻止其重复驱动同一技能事件路由。

## 跨生命周期上下文

ApplyEffectsIntent、Clip、闪现/射线、AoeComp、Buff实例、召唤生命周期均捕获并传递SkillOrigin，包含原施法者完整EntityId；不在命中时从CurrentSkill猜身份。投射物命中专用上下文仍只在物理hit边界有效，技能事件根没有通用分裂资格。HitIndex属于DamageResult，非伤害端口为0。

技能事件Source沿事件的真实结算来源，Target为实际受害目标；完成为施法者/最终合法目标，Point/Direction为实例最后瞄准；投射物终结Target为空、Point为终点、Direction为入射终止方向。来源失效时不把同索引替代实体当成旧来源；失活/死亡来源不启动新的技能事件回调，既有Buff快照周期按原契约继续。

## 作者效果范围与验收

本批将受击、击退、击倒、无敌、击飞、向点拉动及护盾作为具名通用组件节点，不按技能写专用工具。换弹/木桶等当前分支缺失或产品绑定能力留在正式内容迁移任务，不新增假接口。事件/伤害结果在网页可检查，测试场景能编辑护盾和伤害倍率以验证计算。

Root/Attack运动策略接收外部HitDelta，仍由共享Locomotion与Collision解析；击退/拉动无需隐式先进入受击状态，死亡状态继续禁止。结算类型切换保留节点身份、适用连线和启用状态，用新类型默认字段替换旧字段；撤销可恢复原类型和配置，不保存多套互相污染的参数。

验证包含严格字段/端口、JSON/发布往返、所有伤害阶段与过量、无敌/死亡/纯护盾、最近Cast确认、真实hit/crit/kill/completed/终结事件、递归抑制与延迟传播、所有传递路径的原始Origin，以及网页编辑/发布→宿主/Unity同文件对照和完整回归。
