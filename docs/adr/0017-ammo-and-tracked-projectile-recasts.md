# ADR-0017 弹药守卫与追踪弹重施放

日期：2026-11-15。状态：已实施（弹药与追踪弹重施放）；木桶链爆另行迁移。关联H06/N02/M02。

历史`fae7872`的AmmoComp/BuffArenaPlayerComp与资产表明确：普通射击扣1弹、缺弹走Reload，Reload在1.1秒补60弹、总长1.15秒；传送弹配置追踪Owner，第二次输入先处理仍在飞行的弹，受阻保留弹体并消费本次输入。旧WarpSkill仅作分支闸门，实际没有播放其引用时间轴；它不能直接被解释为播放该引用技能。

本批将弹药容量/初值放在角色与预览配置，当前值属于AmmoComp。施放节点保存ammoCost/fallbackSkill，以及recastProjectile/recastSkill/requiredProjectile；引用独立校验，发布包含替代技能闭包，禁止递归。SkillCastComp先解析当前可用分支，再统一校验真实技能的冷却、能量、弹药、活动与目标，成功才扣费。有限连发逐段付费时同时检查能量和弹药，任一不足都不部分扣费。

ProjectileDefinition.trackingKey与TrackedProjectilesComp表示来源最近一枚具名弹体，保存完整EntityId；终结/回收/Owner清理时解除对应映射。多发同key只追踪最后实际生成的弹体，旧弹回收不能清除新弹。传送是通用效果节点teleport-projectile，读取实际弹体、验证角色地面净空、请求Motor位移并立即消费成功弹体；受阻只发失败事件，保留弹体，施放输入已消费。直接播放此操作的技能需要requiredProjectile守卫。

旧传送分支迁移为显式、零费用的独立操作技能，不复用旧闸门指向的双发时间轴；记录迁移身份与行为差别。普通技能、AI、网页预览、Arena/F6/F7共用上述模块，不恢复BuffArenaPlayerComp或专用传送管理器。

原ProjectileMotion的Accelerate不是恒加速度：速度为`2 * Speed * age / (age + MotionParam)`，按固定步起点采样。新增具名ramp轨迹/rampTime参数保留该规则，接触/反射子步沿用本步采样速度；现有线性加速度模式继续独立。正式传送弹使用rampTime=5，网页追踪重施放示例使用线性加速度，均由JSON显式选择。

验收包括延迟换弹/中断、失败零扣费、资源联合支付、递归/坏引用/发布闭包、实际弹体位置与净空、受阻重试、完整EntityId/旧弹回收、新局清理、网页编辑/存储/发布以及MCP实际Unity运行。木桶死亡链爆和完整13技能/资源另作下一节点，不在本批假称完成。
