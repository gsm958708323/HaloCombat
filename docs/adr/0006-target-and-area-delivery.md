# ADR-0006：目标传递、范围结算和持续区域

日期：2026-11-14。状态：实施中。

## 节点与领域映射

新增 delivery 节点，mode=target/area。target 的 recipient=target/self，向当前目标或施法者投递 apply 端口效果包；没有目标时不执行。area 的 center=point/target/source，明确从事件点、目标位置或施法者位置取中心；缺上下文时不猜测其他中心。

area persistent=false 时为瞬时圆查询；persistent=true 时创建独立 AoE Actor，duration、interval、pulseOnSpawn、maxTargets 控制寿命和脉冲。apply 端口为每次脉冲效果，enter/exit/stay/expire 端口对应现有生命周期。target/瞬时区域不支持持续区端口；切换模式时网页删除失效连线，保留可复用参数，撤销可恢复。

伤害结算增加 scope=target/area；area 时按照 center/radius/maxTargets 复用同一个圆查询效果包装 DamageEffect。next 端口在本结算完成后执行一次，不因范围内目标数量重复执行。数组/边的显式顺序及重复执行项必须保留。

## 执行、来源与时间

- 瞬时范围不建实体，经查询→World.Deliver→EffectPipeline 投递，完整 EntityId 在每次执行前重新解析，过滤阵营、死亡、投射物和 AoE。每次调用拥有自己的目标快照，不能复用全局可变缓冲；maxTargets=0 表示全部，正数按稳定实体顺序取前 N 个。
- 持续区快照保存本次 Power，脉冲、占用事件、到期效果继续传递倍率。Pulse 的 Source 为施法者；Enter/Exit/Stay 的 Source 保持原 AoE Actor（Buff 来源租约依赖它）；Expire 的 Source 为施法者，Target 为空，Point 为区域中心。
- AoE 寿命为 [0, duration)。生成时可立即脉冲，后续发生在 interval 的整数倍，等于 duration 的时点不再脉冲。同一逻辑步跨过多个合法触发点时按真实次数补齐；不在寿命外补发。Occupancy 每个活动逻辑步采样一次，不假装连续碰撞。
- 自然到期清理 Exit 后执行 Expire 一次；Owner 死亡/卸载清理执行 Exit 与回收，不冒充自然到期。必须先标记结束再投递清理事件，防止重入重复结束。生成/脉冲/占用/到期均由实际事件记录，网页只显示只读快照。
- 删除 AoEPulse 的静态 32 项缓冲与周期“一步最多一次”旧限制。原 Aura 来源/离开语义保留，并运行既有回归确认影响。
- `EntityRegistry.CopyActiveActors()` 原实现返回跨查询复用的 scratch，范围事件中的嵌套查询会改写外层遍历；修正为真正独立快照。此修正影响 World 各阶段读取，但不改变阶段顺序，必须完整回归结算/死亡/销毁/位移与场景。

## 验收

命中点爆炸多目标、半径编辑、目标与自身传递、中心缺失、阵营/死亡过滤、超过 32 目标、嵌套查询不串目标、顺序/重复效果、蓄力倍率继承、周期小于步长、到期/中断/Owner 死亡/重载清理，以及浏览器发布和 Unity 同文件对照。
