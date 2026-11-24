# ADR-0022：JSON 视图绑定与由逻辑时钟驱动的 Animator

- 日期：2026-11-15
- 状态：已采纳并验收
- 关联：N03、N08、M06；承接 ADR-0021。

## 决策

`runtime.views` 保存具名 Prefab 资源键、缩放、局部朝向及语义到 Animator 完整状态路径的映射。Actor 的 `view` 引用该目录或现有内置程序化模型。Config 校验协议并复制为只读定义；Unity 在候选会话启动前解析 Resources、检查 Animator 与状态。缺失绑定拒绝整次重载，保留当前局。

Timeline 增加可选 `animation` 语义键，导演只将该键、完整 CastId、动作时间和总长输出到表现快照。Animator 不拥有玩法时钟，不通过 AnimationEvent、状态转换或 root motion 驱动技能。动作按 Timeline 归一化相位采样；身体循环和受击按 ActorTime 采样，因此角色独立顿帧冻结对应动画。死亡动画使用现有死亡后 wall-time 清理窗口播放；Arena 暂停同时冻结死亡动画，重复采样只计算真实经过的 wall time。相同技能连续施放通过 CastId 区分；循环相位持续递增，不能在每帧重置到零。

原角色实际绑定的是 `CharacterAnimator`，并非另一个 `GunnerAnimator`。从本地历史 `fae7872` 仅恢复两个人物和枪械的资源依赖闭包，保留 GUID；两个角色移入 Resources。删除 Prefab 中旧 `BuffArenaViewAnchor`，删除 Controller 的自动过渡，由新表现路径显式采样。材质保持共享资源，闪白通过 MaterialPropertyBlock，不修改或销毁共享材质。旧 SO、PresentHub 和 GameFlow 不恢复。

## 数据流

网页运行配置/Timeline → 严格 JSON → Config 只读视图目录 / Core 动作语义 → Unity 候选资源校验 → ViewFactory → ActorView → Animator 相位采样。

语义 `Roll` 根据本次移动方向选择 `RollForward/Back/Left/Right`，其他动作键直接查映射。身体状态使用 `Stand`、`MoveForward/Back/Left/Right`、`JumpAir`；受击使用 `Hurt0/Hurt1`，死亡使用 `Dead`。资源目录显式提供这些身体状态，动作键必须能在该目录映射。无动作键的技能不猜测为 Fire。

当前状态绑定要求第0层的单个 AnimationClip，不包含 BlendTree；当前原角色的 Controller 全部满足这一条件。Animator.speed=0 时实测 AnimatorStateInfo.length 为 Infinity，因此身体与受击时长取源 AnimationClip.length。动画字典键属于大小写敏感的内容令牌，使用局部 JsonDictionary 命名策略保留原样，不修改工程其他 JSON 命名契约。

## 验收边界

验证资源闭包、缺失资源/状态拒绝、相位、同技能重播、动作速度、独立顿帧、受击/死亡、回收和重开。网页保留示意图预览，Unity Arena 负责本节点真实骨骼动画验收。历史十三技能、全套特效/音频和完整运行包仍属于后续节点。

证据：[角色动画验收](../验证/2026-11-15-actor-animation.md)。原材质引用的缺失 emission 纹理未包含在历史提交，已清除无效引用且保持发光关闭；其余原反照率/金属度/遮蔽纹理保留并转换为当前 URP Lit。全套原表现继续由 N08 跟踪；取得原缺失纹理并完成原表现对照后再恢复对应发光效果。
