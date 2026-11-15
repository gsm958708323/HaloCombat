# ADR-0027：可组合游走 AI 与生成成长

- 日期：2026-11-15
- 状态：已采纳，2026-11-15完成本节点验收
- 关联：N01/N02，fae7872的WanderShooter、角色定义与ConfigurePlayer/ConfigureEnemy。

## 边界

角色基础属性仍由RuntimeActorDefinition定义。可选spawnStats仅在Arena成功生成角色后、PublishSpawn之前应用，ordinal使用累计成功生成敌人数而非当前存活数；重开从0开始。DTO经独立编译转换为领域采样规则，不能写回共享角色定义或在每帧重复增长。普通演示/SkillLab的独立试验属性保持显式初值。

profile先抽攻击整数偏移，再可选抽速度曲线。声明profile时即使攻击范围为0仍消费原随机抽样；未声明时不抽样，避免无关作者工程随机序列改变。速度为`value*scale/(value+divisor)+offset`，value为base加整数随机偏移。

原敌人行为拆为BtParallel中的ActRandomWander和ActPeriodicCast，先游走抽样再射击抽样。Dead/Downed/Stunned通过既有选择器守卫冻结子节点；ActorTime冻结整个行为。定时施法仍走SkillCastComp，正常消耗和冷却不能绕过。每角色克隆叶子保存独立计时/朝向，Shared definition无运行态。

wander-shooter保留会话指定目标，不额外挂Perception改变旧目标选择/随机序列。原+Z yaw到当前+X yaw按`new=90-old`转换，因此出生朝向为随机角，随机换向符号随坐标约定翻转。相同已接受站位和抽样顺序保留原数值语义；现有矩形障碍试验场与原栅格地图不宣称整局坐标逐点相同。

## 验收目标

数值/整数边界、生成前事件可见、累计成长/补怪/重开、首2秒换向/首3秒施法及后续区间、独立克隆/停止/受击/冷却拒绝、相同JSON/种子/输入重放，以及网页编辑正式JSON后Unity读取。

验收记录见`docs/验证/2026-11-15-wander-growth.md`。跨运行环境的碰墙浮点投影使用位置0.1毫米/朝向0.001度容差，实测最大0.072毫米/0.000459度；成长、施放事件与伤害无差异。不将浮点投影容差描述成逐位确定性保证。
