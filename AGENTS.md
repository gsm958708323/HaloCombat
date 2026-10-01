# AGENTS.md

本文件只定义 AI 修改本仓库时必须遵守的工程规则。项目当前实现、目录、运行命令、测试结果和内容清单请阅读 [README.md](README.md)；README 中的事实描述不自动构成额外架构禁令。

## 修改前

- 先阅读本文件、`README.md`、目标代码的调用链、相关配置和测试。
- 先确认当前实现，再修改；不得把提案、旧文档或未运行的命令写成已验证事实。
- 保留工作区已有修改，除非用户明确要求回退。

## 架构边界

- 核心采用纯 C# OOP 组件架构；逻辑类不继承 `MonoBehaviour`，由薄适配层驱动。
- `Combat.Core`、`Combat.Demos` 保持零 Unity 引用。不擅自引入 ECS、全局管理器、`GameObject.Find`、字符串扫描实体、新消息系统或网络框架；新增依赖前先检查现有依赖。不限制 .NET `System` 或 Unity Input System 的正常使用。
- `Actor` 只持有 `EntityId`、组件和生命周期；业务规则放在组件或服务中。
- `*Comp` 承载有实体归属的逻辑；`*Service` 承载跨实体、全局或无需 Actor 的能力。
- 同 Actor 依赖默认通过 `GetComp<具体Comp>()` / `TryGetComp<具体Comp>()` 查找。
- 逻辑组件统一继承抽象 `Comp`，生命周期为 `OnAttach`、`OnDetach`、`Tick`；不要建立 `XxxComp : IXxx, IComp`、`ITagRead`、`ITagWrite` 等接口森林。
- 接口仅用于确有多实现、跨边界替换或通用结算策略的场景，例如 `IEffect`、可替换查询/随机源、`IIncomingDamage`。不得以“禁止所有接口”为理由删除合法边界接口。
- 公开 API 参数保持专用、最小化；不要用臃肿通用 Context 代替派生类专属参数。
- `IEffect.Apply(ref EffectContext)` 是统一效果管线协议。Effect 专属配置放在具体 Effect/定义中，不为少数 Effect 无边界扩张 `EffectContext`。

## 状态与连招

- 活动状态只保留 `Root`、`Attack`、`Hit`、`Knockdown`、`Dead`；技能细过程由 Timeline 表达，跳跃由移动组件和 Tag 表达。
- 连招使用配置边表 `ComboEntry`：输入、`PreSkills`、`RequiredTags`、优先级、目标技能和 Timeline。匹配只读上下文和 Tag，选择最高优先级合法边。
- 不得恢复 `ComboStateComp`、`ComboStage`、`RequiredStage`、`RequireHitConfirmed` 或独立连招阶段状态。命中资格用普通 `ComboConfirm` Tag/Buff 表达；Cancel 由代码自动检查，不在每条边重复配置。
- `PreSkills` 支持多个来源；空前置表示起手。播轴期间匹配当前技能且必须满足 Cancel，不能用起手回退绕过；自然结束后先用最近成功施法历史匹配后续边，无合法边再尝试起手。起手不自动要求 Cancel，仍检查显式 `RequiredTags`。
- 输入采用缓冲预输入；只有匹配且成功施法才消费，失败不消费。`ComboComp` 不维护命中计数、阶段或计时窗口，也不订阅伤害事件；施法历史归 `SkillDirectorComp`，`CurrentSkill` 仍表示正在播放的技能。
- 确认 Tag 与延迟命中资格（`LastCastId`）的有效期严格限制在播轴期间：任何结束都清除，包括时间轴自然结束（`FlushTimeline`）与受击、倒地、死亡、手动中止、卸载（`Stop`）；一次完整边表判断或一次成功施法也会消费确认。因此「播轴期间命中、播完后接招」不再成立，连招必须在播轴期间通过取消窗完成。不恢复独立连招计时器；重复命中不叠层，旧施法的伤害不得授予新施法资格。
- 受击、倒地、死亡、手动中止和卸载清理输入、当前技能、待处理技能及临时位移请求；自然结束保留最近一次成功施法历史。
- 新技能优先新增配置（ComboEntry、Timeline、Effect、Projectile/AoE 定义），不要为旧技能增加专用分支。

## Tag、Effect 与结算

- Tag 是可叠加、可跨系统查询的资格标记。写入主要来自状态进出、Timeline Effect 和 Buff；Tag Lease 必须按明确来源成对释放，移除一方不得减少其他来源。
- Condition/连招匹配只读 Tag 和只读上下文，不修改世界。
- 技能 Timeline 负责取消窗、无敌帧、位移请求、判定、Cue、投射物/AoE 和效果触发。逻辑时间以 Timeline 为权威，表现层不得反向驱动战斗状态。
- Timeline 中的 `Clip` 与 `Payload` 职责必须分开：`Clip` 是带 `[Start, End)` 区间的持续行为，由 `ClipKind`（当前为 `CancelTag`、`Move`、`Hitbox`、`IFrame`）对应的 handler 管理 `Open`、区间内 `Tick` 和 `Close` 生命周期；它表达取消窗、位移、持续判定或持续资格，不直接承载任意一次性效果包。Clip 被打断时也必须执行 `Close`，按原来源释放 Tag、Hitbox 或移动控制，不能留下状态。
- `Payload` 是单个 `Time` 的离散触发点；时间轴推进到该时间时只投递一次其 `IEffect[]`，数组下标决定同一时间点的投递顺序。它用于 Cue、生成投射物/AoE/召唤物及其他一次性效果，不拥有持续区间和 `Open/Close` 生命周期；需要持续生效的逻辑必须建模为 Clip 或独立运行时实体/Buff。
- Clip 与 Payload 都属于 Timeline 的只读运行时定义，按时间轴实例播放；不得把表现层 `AnimationClip` 当作战斗 Clip，也不得让表现回写 Clip/Payload、HP、位姿、技能状态或 Tag。新增 `ClipKind` 必须同时实现对应 handler、打断清理和配置校验；Payload 的 Effect 顺序和失败语义遵循统一 `EffectPipeline`，不得绕过 `CombatWorld.Deliver`。
- 连招边要生效，前置技能的时间轴必须配置 `CancelTag` 取消窗：播轴期间只有取消窗打开时 `ComboComp` 才进入边表匹配，窗口未开时输入只排队、不消费、不匹配。没有取消窗的技能播完即清确认，接不上任何后续边。连招匹配默认检查 `Cancel`，不需要在 `ComboEntry` 里额外配置。
- 统一伤害边界为 `CombatWorld.Deliver -> EffectPipeline -> DamageEffect`；新增跨实体逻辑优先 Intent，但保留服务内部已有直接 `World.Deliver`。
- 属性、生命、伤害和 Buff 修改由专职属性组件承担，不混入 `SkillDirectorComp` 或状态组件；HP 通过 `AttributeSet.SetBase(AttrId.Hp)` 修改。
- Attack 期间普通移动和 Timeline 位移可以并存；Root/Jump 的普通移动由移动组件负责，技能代码不得直接写 Transform。

## 实体与服务

- 长生命周期跨实体关系使用完整 `EntityId(index, generation)`；触发时通过 World 查询，实体失效按无来源/无目标处理。短暂调用参数可用 Actor 引用，但不得形成长期强引用。
- 使用注册表和帧末销毁队列管理实体；投射物、持续火池、召唤物等独立生命期对象使用独立 Actor 与对应组件。
- 组件不得长期持有全场 Actor 列表；瞬时范围效果可直接投递到 AoE/效果管线，不强行建实体。投射物和 AoE 运行时体不作为普通战斗目标。
- 不规定唯一的 `CombatWorld` Tick 阶段顺序。调整阶段时必须验证命中、伤害、Buff、死亡、位移和销毁行为；当前顺序属于实现事实。
- 表现层只读取核心组件和事件，不回写 HP、位姿、技能状态或 Tag。不要擅自拆分已有程序集边界或引入新的架构层。
- 表现按 `PresentHub -> PresentActor -> PresentComp` 组织，每个组件只负责一类行为，可直接使用 Unity API，不改造成引擎无关层。命名空间前缀与程序集名保持一致；`Game` / `Presentation` 保持 `Combat.Unity` 内逻辑分区，不另建 asmdef。
- `Combat.Config` 将 SO `Bake()` 为运行时定义；Arena 使用 SO 内容，CLI 使用独立代码表。Arena 内容无效时启动失败，不添加代码回退。每个 ScriptableObject 类型放入同名 `.cs` 文件。
- Arena SO 入口固定为 `BuffArenaDatabaseAsset -> ArenaRulesAsset / ArenaPresentationAsset / ArenaContentManifestAsset`。Rules 只放对局规则，Presentation 只放相机，Manifest 列 Actors、Skills、Combos、Timelines、Projectiles、Aoes、Buffs、Cues。
- SO 之间的编辑期关系使用强类型资产引用；`SkillIdValue`、`TimelineIdValue`、`SpecId`、`BuffId` 只是稳定运行时身份。禁止让策划手填跨资产 ID 作为唯一引用，也禁止用数组下标作为身份。
- `Bake()` 每次生成完整运行时快照和类型明确的 Catalog/Dictionary，不保存 ScriptableObject 引用或跨次 Bake 缓存。Bake 必须检查 null、非零/重复 ID、引用是否在当前 Manifest、Timeline/Effect 时间范围和递归 Effect 引用，并在错误中包含资产、字段和数组下标。
- 作者资产放在 `Assets/Combat/Config/Authored/Arena` 及其分类目录；`Generated` 不作为手工维护 Arena 内容目录。迁移资产必须保留 Unity GUID；带人工 Prefab 绑定的 Cue 不得删除重建。
- AI 行为树负责移动和施法编排，不直接结算或调用 `Director.Stop`，树按 Actor 克隆。

## 变更与验证

涉及活动状态、连招语义、结算边界、跨实体生命周期、程序集边界或内容源的修改，必须在变更说明中列出影响，并先取得新的架构决策。不要为了假设中的未来需求增加抽象；遵循 KISS、SOLID、DRY。

从需求本质出发，优先简单、可靠、可维护的方案。先检查现有代码、依赖和测试；不为兼容长期保留废弃方案，不为少量重复增加过度抽象，不用未经验证的新设计替换可运行系统。

涉及 Core 的修改至少运行 `dotnet build Combat.csproj` 和 `regress`。涉及 Unity 代码、资产或场景时，还要运行 Unity 编译及相关 EditMode/PlayMode 测试，并检查生成资产已写入磁盘。最终报告必须区分已运行的验证和未获得运行证据的项目。
