# HaloCombat

纯 C# 动作战斗核，Unity 只负责表现与关卡。逻辑可脱离引擎，用 `dotnet run` 做回归；正式对局入口是 `Assets/Scenes/Arena.unity`。

当前基于 Unity 6（`6000.0.32f1`）+ URP，命令行目标框架为 `net8.0`。

本文介绍项目现状、运行方式和验证入口。AI 修改约束见 [AGENTS.md](AGENTS.md)，玩家技能配置与行为详解见 [玩家技能实现详解](docs/玩家技能实现详解.md)。

## 当前实现概览

- `Combat.Core` 零 Unity 引用；Demo 和回归可脱离场景运行。
- 效果通过 `IEffect.Apply(ref EffectContext)` 进入 `EffectPipeline`，伤害由 `World.Deliver` 统一送入 `DamageEffect`。
- 编辑器 ScriptableObject 在启动时 `Bake()` 成纯 C# 运行时目录，运行时不直接读取 SO。
- 表现层读取核心组件并订阅事件，不修改血量、位姿或技能状态。
- 当前输入缓冲为三条 FIFO，角色时间窗口默认 `0.8s`；只有普攻支持按住连射。
- 当前位移链路为 `Request*` → `Locomotion.Integrate`；yaw `0` 朝 Unity 的 `+Z`，局部偏移 `+Z` 为前、`+X` 为右。
- 当前 `Combat.Unity.Game` 与 `Combat.Unity.Presentation` 是 `Combat.Unity` 程序集中的逻辑分区；它们不是独立程序集。
- Arena 与 CLI 使用独立内容源：Arena 使用 `BuffArenaDatabaseAsset` 烘焙资产，CLI 使用代码表，二者没有自动比对。
- 玩家按键映射留在 `BuffArenaSession.ApplyInput`，连招入口唯一由 `ComboEntryAsset.InputAction` 配置；技能资产不再承担重复输入映射。
- Unity 资源脚本目前按“一种 ScriptableObject 类型对应一个同名 `.cs` 文件”组织，以保证 MonoScript 能正确落盘。
- Arena 敌人当前使用代码中的 `WanderShooter` 叶子和节奏参数，未纳入 SO 配置。

## 分层与程序集

当前命名空间前缀通常与所属程序集名一致。模块 ≠ 程序集：`Combat.Unity.Game` / `Combat.Unity.Presentation` 是 `Combat.Unity` 程序集内的**逻辑分区**（同程序集内没有编译期隔离），不是独立程序集。

| 命名空间 | 程序集 |
| --- | --- |
| `Combat.Core` | `Combat.Core` |
| `Combat.Config` | `Combat.Config` |
| `Combat.Demos` | `Combat.Demos` |
| `Combat.Unity.Game` / `Combat.Unity.Presentation` / `Combat.Unity` | `Combat.Unity` |
| `Combat.Editor` | `Combat.Editor` |

| 模块 | 职责 | 隔离 |
| --- | --- | --- |
| `Combat.Core` | 纯 C# 战斗核。实体、时间、效果、技能、弹体、AoE、Buff、行为树 | 程序集；零引擎引用 |
| `Combat.Config` | Unity SO 配置，`Bake()` 成运行时定义 | 程序集 |
| `Combat.Unity.Game` | 对局会话、50Hz 逻辑步、暂停、胜负、输入路由 | 逻辑分区 |
| `Combat.Unity.Presentation` | `PresentHub → PresentActor → PresentComp` 视图组件 | 逻辑分区 |
| `Combat.Unity` | 场景入口、Input System、HUD、VFX / Floater 池 | 程序集 |
| `Combat.Demos` | 纯 C# 逻辑 Demo，Editor-only | 程序集；零引擎引用 |
| `Combat.Editor` | 数据库生成、场景检查、Demo 校验 | 程序集；Editor-only |

### 表现层：`PresentComp`

表现层的解耦单元是 `PresentComp`——**表现层角色的组件**。

- **表现行为按组件横向拆开**，而不是堆进一个视图类。每个 `PresentComp` 只管一件事（位姿跟随、动画状态、Buff 特效、受击反馈、飘字锚点……），再组合进 `PresentActor`。
- **组件可以直接写 Unity 逻辑**（`GameObject` / `Transform` / `Animator` / `ParticleSystem`……）——这是有意设计，不是技术债。
- **数据来源是 `Combat.Core` 的 comp**：在 `SyncLogic(world)` 里经 `Self.TryLogic(world, out var actor)` 取到 `Actor`，再用 `actor.TryGetComp<T>()` 读取逻辑状态。表现层不回写逻辑状态。

`Combat.Core` 与 `Combat.Demos` 的 asmdef 设置了 `noEngineReferences: true`，由编译器检查零引擎引用。表现层直接使用 Unity API，`Game` / `Presentation` 共用 `Combat.Unity` 程序集。

源码位置：

- 逻辑核：`Assets/Scripts/Combat/Core`
- 配置：`Assets/Scripts/Combat/Config`
- 对局 / 表现 / Unity 胶水：`Assets/Scripts/Combat/Unity/Game`、`Unity/Presentation`、`Unity`
- Arena 作者资源：`Assets/Combat/Config/Authored/Arena`

## 战斗核

`CombatWorld` 持有时间、实体表、意图队列、事件总线、效果管线，以及弹体 / AoE / 命中服务。当前实现的 Tick 顺序如下；这是代码现状，不是不可调整的框架规则：

1. Actor 组件 Tick
2. 命中前位移积分
3. 弹体与近战命中
4. Drain `ApplyEffectsIntent`
5. Flush Timeline Clip
6. AoE 脉冲 / occupancy
7. Buff 周期
8. 命中后位移积分
9. Flush Despawn

Hitstop 只推进 wall time，暂停逻辑帧。实体用 `EntityId(index, generation)`。季节 1/2 的 Blueprint 由 `FighterActorFactory` 组装玩家、敌人、木桩、召唤物、弹体和 AoE；正式 Arena 走 `BuffArenaActorFactory`，按 `BuffArenaActorDef`（team / 半径 / 弹药 / HP / Atk / 速度 / 暴击）组装。

### 技能与活动

- Timeline Clip：`CancelTag` / `Move` / `Hitbox` / `IFrame`
- Timeline Payload：Cue、生成弹体 / AoE / 召唤物
- 活动机：`Root` / `Attack` / `Hit` / `Knockdown` / `Dead`，各自带位移与朝向策略
- 连招由 `ComboEntry` 边表解析，使用 `PreSkills`、`RequiredTags` 和优先级；`ComboStateComp` 已删除，命中资格由 `ComboConfirm` Tag/Buff 表达，播放期间的 Cancel 由代码自动检查。起手边必须显式配置，`InputToken` 不会隐式生成连招边。
- 玩家：季节 1/2 与 Arena 共用 `ComboComp` 解析输入；Arena 的玩家 Actor 引用 `ComboTableAsset`，每个起手和分支都在表中显式声明。成功施放后消费三条 FIFO 中的一条，窗口为角色时间 `0.8s`；受击、倒地、死亡清空输入、停止当前时间轴并重置技能。
- AI：感知写黑板，行为树当前只编排移动和 `PlaySkill`，不直接结算；Arena 的 AI 当前不调用 `Director.Stop`，树按 Actor 克隆
- Arena 敌人的树是**单个 `WanderShooter` 叶子**（游走 + 朝目标 + 定时施法），没有 selector / sequence；`BtFactory` 那套完整代码树服务于季节 1/2

### 效果与状态

效果包覆盖伤害、Tag、Buff、硬直、击退、击飞、击倒、无敌帧、Cue、生成弹体 / AoE / 召唤物。

- 属性：`Hp` / `MaxHp` / `Shield` / `Atk` / `Def` / `MoveSpeed` / `CritRate` / `DmgDealMul` / `DmgTakenMul`，Modifier 为 Add / Mul / Override
- Tag：可叠层计数，用于 Grounded、Cancel、Casting、Stunned、Invincible、Downed、Dead 等
- Buff：叠层、互斥、周期、驱散；Aura 用地板 occupancy 的 Enter / Exit 驱动

### 实现状态与限制

- `BuffComp.Inst` 已保存 `EntityId SourceId`，执行效果时通过 World 临时解析来源；旧文档中“长期保存 Actor”的迁移项已过时。但按来源驱散仍使用整数 key，该路径尚未完全迁移到完整实体身份。
- `EffectContext` 当前包含多个通用字段；具体 Effect 的专属配置由 Effect/资产持有。
- `Actor.TryGetComp<T>()` 支持接口查询，供通用结算边界策略使用。
- 播轴期间，`ComboComp` 使用当前技能并自动检查 Cancel；自然结束后使用 `ComboSourceSkill` 尝试后续边，无合法边才尝试起手。成功施法清理确认 Buff。上述已实现行为不代表整个连招迁移验收已经完成。

## 内容管线

两条路径，互不依赖：

**Arena（当前运行时内容路径）** —— `BuffArenaDatabaseAsset.Bake()` → `BuffArenaData`。当前内容不可用时启动会失败（抛异常并带上 `LastContentError`），实现没有代码回退。作者资产在 `Assets/Combat/Config/Authored/Arena`：

| 目录 / 文件 | 内容 |
| --- | --- |
| `ArenaRules.asset` | Arena 规则、生成节奏、随机种子、运动策略 |
| `ArenaPresentation.asset` | 相机取景 |
| `ArenaContentManifest.asset` | Actors、Skills、Combos、Timelines、Projectiles、Aoes、Buffs、Cues 内容清单 |
| `Actors/BA_*.asset` | 每个 Blueprint 的 Actor 数值和角色专属 ComboTable |
| `Skills/SK_*.asset` / `Timelines/TL_*.asset` | 技能和时间轴 |
| `Projectiles/PD_*.asset` / `Aoes/AD_*.asset` | 弹体与 AoE 定义 |
| `BuffArenaDatabase.asset` | Arena 根入口，只组合 Rules、Presentation、Manifest |

作者数组只表示内容清单，不表示运行时顺序，也不能用数组下标充当 ID。`Bake()` 会检查 ID 唯一性、强类型引用是否在当前 Manifest、Timeline/Effect 的时间与 null 槽位，并生成类型明确的运行时 Catalog；运行时不保存 ScriptableObject 引用。

**输入映射不进技能 SO**：按键映射见 [BuffArenaSession.ApplyInput](Assets/Scripts/Combat/Unity/Game/BuffArenaSession.cs)，连招表的 `ComboEntryAsset.InputAction` 是唯一入口来源。同帧优先级为 Roll、Fire4、Monkey、Homing、Fire5、Fire3、Fire2、Fire1，超过容量时保留前三项；不同帧遵循 FIFO。按住普攻独立于队列。耗弹、时间轴、动画、传送弹开关、空弹回退 `FallbackSkillIdValue` 仍在 SO 上。F5 重载当前 Arena，详见[玩家技能实现详解](docs/玩家技能实现详解.md)。

`Bake()` 启动前做引用校验：每个技能必须命中时间轴、空弹回退必须命中技能、工厂按名索取的 blueprint 必须存在；任一条不通过就返回 null，场景启动即失败。

**CLI / 逻辑 Demo（代码表驱动）** —— `CodeCombatContent`，内嵌第一 / 二期默认表，不依赖 SO（命令行加载不了 Unity 资产）。已覆盖内容包括近战连段、火球灼烧、火地叠层、闪避无敌帧、追踪弹、光环减速、近战 / 远程 / 守卫 AI 和召唤物。Arena 是 Unity 场景内容，**没有 C# CLI 用例**；它的回归在 PlayMode 冒烟测试里。

## 表现与对局

`LogicTicker` 以 `1/50s` 固定步长推进逻辑，渲染用 remainder 插值。`GameFlow` 状态为 Title → Loading → Arena → Result。

正式 Arena 由 `BuffArenaBootstrap` 创建 `CombatWorld`、`PresentHub` 和 `BuffArenaSession`。表现层订阅 `EvEntitySpawn`、`EvCue`、`EvDamage`、`EvImmune`、`EvHeal`、`EvHitstop`，用对象池播放特效和飘字。

逻辑验证场景不包含可操作表现层。第二季 16 个 Demo 场景仍是纯逻辑验收；Arena 另有 PlayMode 冒烟测试覆盖 SO 内容可烘焙、五个技能的出弹 / 出桶、技能 1 命中扣血，以及 8 个按键各自起对应技能。

## 运行 Demo

仓库根目录：

```powershell
dotnet run --project Combat.csproj -- season
dotnet run --project Combat.csproj -- --category TagInput tag
```

| 参数 | 内容 |
| --- | --- |
| `tag` | Tag 计数与输入缓冲窗口 |
| `attr` | 属性 Modifier 与 HP 上限 |
| `buff` | Buff 叠层 / 周期 / 驱散 |
| `motor` | 活动机位移策略 |
| `clip` | Timeline Clip / Payload |
| `melee` | 近战命中与伤害 |
| `proj` | 弹体与火地 AoE |
| `season` | 第一期总装（默认） |
| `knock` | 击倒 |
| `dodge` | 闪避无敌帧与 Hitstop |
| `aura` | 光环 occupancy 与追踪弹 |
| `bt` | 行为树编排 |
| `perc` | 感知写黑板 |
| `enemy` | 敌人 AI |
| `summon` | 召唤物生命周期 |
| `season2` | 第二期总装 |
| `lesson` | 第三期课程验收 |
| `regress` | 回归套件 |

无人值守 / CI 跑法（要求全过程零弹窗）：先 `dotnet build`，再直接跑产物并重定向输出、加超时，只用退出码判定成败。
未处理异常一旦逃出进程入口，CLR 会 terminate 进程（`0xE0434352`）并拉起 Windows JIT 调试器框和"应用程序错误"框，把自动化挂到有人点击为止。
`Program.Main` 已把任何 demo 失败转成 `DEMO FAILED:` + 退出码 `1`，`NoPopup.Arm()` 再用 `SetErrorMode` 关掉原生 fault 的错误框；`SeasonTwoDemo.Regression` 逐个 demo 兜底，一个失败不再吞掉后面的用例。

```powershell
dotnet build Combat.csproj -v q --nologo
$env:DOTNET_EnableDiagnostics = '0'
$p = Start-Process .\bin\Debug\net8.0\Combat.exe -ArgumentList 'regress' -NoNewWindow -PassThru `
     -RedirectStandardOutput out.log -RedirectStandardError err.log
if (-not $p.WaitForExit(600000)) { $p.Kill(); throw '超时已 kill' }
"exit=$($p.ExitCode)"; Get-Content err.log -Tail 20
```

默认 `season` 验收：G1 近战 + 刀光 Cue + 火球灼烧、G2 火地叠 3、受击停轴、Bake 清缓存、死亡清弹圈。

Unity Editor 中对应场景在 `Assets/Scenes/HaloCombat`。Play Mode 下 `HaloCombatDemoRunner` 会跑同一套 Demo，并把结果写到 Console。正式对局场景是 `Assets/Scenes/Arena.unity`。

`FinalVerification.Run` 只调用 `HaloCombatDemoSceneBuilder.VerifyAll()` 校验场景，不烘焙数据库、也不跑 Demo。

## 测试

| 套件 | 位置 | 覆盖 |
| --- | --- | --- |
| PlayMode | `Assets/Tests/PlayMode`（`Combat.PlayModeTests`） | 加载 `Arena` 场景跑真实 `BuffArenaSession`：SO 内容可烘焙、五个技能各自出弹 / 出桶、技能 1 命中扣血、8 个按键各自起对应技能 |
| EditMode | `Assets/Tests/EditMode`（`Combat.EditModeTests`） | 全工程 `EffectAsset` 子类都必须有 MonoScript |

PlayMode 用例在 `[UnitySetUp]` 里设 `Application.runInBackground = true`：编辑器窗口失焦会节流主循环，无人值守跑会在第一帧就停住。

测试从磁盘读内容，所以「内存里看着对、磁盘上是坏的」这类问题只有跑测试才暴露 —— 改完生成资产要重跑一次。

## 日志

共享代码走 `Combat.Core.CombatLog`，格式为 `[等级][category] message`。默认 `MinimumLevel` 为 `Debug`，可调到 `Info` / `Warn` / `Error` 减少输出。

- `.NET` 入口注册 `ConsoleLogSink`
- Unity `HaloCombatDemoRunner` 注册 `UnityLogSink`
- Sink 自身异常会被吞掉，不中断战斗流程

可用 category：`TagInput`、`Attribute`、`Buff`、`ActivityMotor`、`ClipPayload`、`MeleeDamage`、`ProjectileAoe`、`SeasonOne`、`Knockdown`、`DodgeHitstop`、`AuraHoming`、`BehaviorTree`、`Perception`、`EnemyAi`、`Summon`、`SeasonTwo`。

`CombatLog.SetCategoryFilter("TagInput")` 只保留该分类；传入 `null`、空字符串或 `All` 恢复全部输出。Unity 可在 Runner 的 `Category Filter` 下拉框切换，Play Mode 立即生效。命令行示例：

```powershell
dotnet run --project Combat.csproj -- --category TagInput tag
```

Unity Console 会给 category 上色；`.NET` Console 保持纯文本，方便重定向。
