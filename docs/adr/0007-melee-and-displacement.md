# ADR-0007：近战与位移攻击

日期：2026-11-14。状态：实施中。

后续状态：初批中“无障碍平面”的限制已由 ADR-0008 补齐矩形障碍、落点净空和连续路径阻挡；当前碰撞语义以 ADR-0008 为准。

## 定义与执行

delivery mode 新增 melee/displacement。melee shape=thrust/fan/circle，length、width、angle、maxTargets、startup、activeDuration 控制几何和时序；displacement movementMode=dash/blink，distance、speed、hitRadius、startup 控制位移。apply 端口为命中效果。

- 近战与冲刺编译成 TimelineClip 原型，受根施放 Payload 的成功触发/扣费调度；每次触发生成自己的 ClipRuntime/handler，开始与结束仍为 [Start,End)，Close 在完成/打断/卸载时执行。单次按完整 EntityId 去重，0 目标上限为全部；不同连发段独立去重。
- 原型只允许从施放入口及其顺序链调度；投射物命中/AoE 事件不能把角色自身的持续动作隐式挂到已经结束的轴，导入时明确拒绝该上下文。apply 内的普通效果仍可组合投射物/区域/伤害。
- 闪现是依附施放触发的延迟 Payload，保存该次目标/点/方向与 Power，由同一 Timeline 播放实例管理待触发队列；不是带任意效果包的伪持续 Clip。轴结束或被打断清空未触发项。
- 冲刺每步按实际覆盖区间请求 Locomotion.RequestSkillDelta，使用当前位置到请求终点的线段 + hitRadius 做扫掠查询；普通位置改变不绕过移动组件。闪现通过 RequestTeleport，按明确落点做一次范围投递，不命中沿途。
- 闪现接受施放前检查有限、距离内的目标点；实际触发时再次检查已选 EntityId/距离，不把无效目标替换成其他实体。当前二维平面没有地形阻挡，障碍合法性属于后续统一碰撞能力，不能声称已验证。
- 位移期间禁止重叠冲刺；编译器拒绝会重叠的连发间隔。近战可重复，重叠实例有独立记录。有限轴保留末段动作后的一个逻辑步恢复时间，以便最后的位移请求在 Attack 运动策略内积分。
- Clip 只按该步与自身区间的交集 Tick，不能因为大步跨越开始/结束而多移动一整个步长。引导和蓄力仍由现有 Timeline/守卫推进；蓄力倍率在动作实例上保存，不修改定义。

## 表现和验证

核心发布只读判定形状事件，网页/Unity 绘制实际运行中的刺击矩形、扇形/圆形范围；移动轨迹来自实际积分后位姿。表现在中断后不得继续保留活动判定。

覆盖形状/方向/边界/前摇/时长/上限/去重，跨步与高速冲刺，精确闪现/无效目标，分段消耗/引导/蓄力，中断、死亡、卸载、重载及关闭生命周期。调整 Clip 调度会影响旧 Clip，必须运行 Core 全回归和 Unity 全部相关测试。

旧 EnemyAiDemo 把只执行一次的 AdvanceFor details 回调当逐帧采样，用结束巡逻的位置冒充整段最近 Home 距离。时间区间修正暴露此误报；将最近距离采样移到每次 Step 后，保留原 0.6m 阈值和 150 步范围，不修改 AI 返回/巡逻参数。
