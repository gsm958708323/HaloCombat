# HaloCombat

纯 C# 动作战斗核，Unity 只负责表现与关卡。逻辑可脱离引擎，用 `dotnet run` 做回归；正式对局入口是 `Assets/Scenes/Arena.unity`。

当前基于 Unity 6（`6000.0.32f1`）+ URP，命令行目标框架为 `net8.0`。

## 设计取舍

- 逻辑与 Unity 解耦：`Combat.Core` 零引擎引用，Demo 与回归不依赖场景。
- 结算路径唯一：效果只走 `IEffect.Apply(ref EffectContext)` 和 `World.Deliver`。
- 数据 Bake 进运行时：编辑器 ScriptableObject 在启动时烘焙成纯 C# 目录，运行时不再读 SO。
- 表现只订阅事件：视图层不改血量、位姿或技能状态。

冻结约定：

- 骨架：`Actor` / `Comp` / `World` / `Time` / `EventBus` / `IntentQueue` / `Pool`
- 技能：Combo 边表 + `Play(skill, timeline)`
- 输入：`InputBuffer` 单槽，窗口 `0.2s`
- 位姿：`Request*` → `Locomotion.Integrate`
- 朝向：yaw `0` 朝 `+Z`（Unity 前向），正角朝 `+X`，`ForwardFromYaw(yaw)` 等价于 `Quaternion.Euler(0, yaw, 0) * Vector3.forward`；局部偏移同坐标系（`+Z` 前、`+X` 右）。旧 2D 极角约定（`0 = +X`）作废，换算为 `90 - yaw`
- 血量：只通过 `AttributeSet.SetBase(Hp)` 修改
- 分层：命名空间前缀 == 所属程序集名；`Game` / `Presentation` 是 `Combat.Unity` 内的逻辑分区，不拆 asmdef
- 内容来源唯一：Arena 只认 `BuffArenaDatabaseAsset` 烘焙出的资产（内容数值是 SO 数据），CLI 只认代码表；两者各自独立，没有生成器，也没有比对
- 玩家按键表留在代码：`BuffArenaSession.ApplyInput` 把 8 个动作硬编码成技能 token，token 必须与各 `SK_*.asset` 上的 `InputToken` 相等。输入映射不进 SO
- 一个 ScriptableObject 类 = 一个同名 `.cs` 文件：Unity 只给与文件名同名的类铸 MonoScript，否则落盘成 `m_Script: 0`、域重载后变 null
- Arena 的 AI 结构与节奏留在代码：敌人树固定为单个 `WanderShooter` 叶子，开火 / 游走区间与 `AcquireRadius` 是代码常量；AI 编排属逻辑，不纳入 SO 配置

## 分层与程序集

**命名空间前缀 = 所属程序集名，无例外。** 模块 ≠ 程序集：`Combat.Unity.Game` / `Combat.Unity.Presentation` 是 `Combat.Unity` 程序集内的**逻辑分区**（同程序集内没有编译期隔离），不是独立程序集。

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

因此：只有 `Combat.Core` 与 `Combat.Demos` 受零引擎约束（asmdef `noEngineReferences: true`，编译器强制）。把表现层改成引擎无关层、或为 `Game` / `Presentation` 新建独立 asmdef，都已明确否决。

源码位置：

- 逻辑核：`Assets/Scripts/Combat/Core`
- 配置：`Assets/Scripts/Combat/Config`
- 对局 / 表现 / Unity 胶水：`Assets/Scripts/Combat/Unity/Game`、`Unity/Presentation`、`Unity`
- 烘焙资源：`Assets/Combat/Config/Generated`

## 战斗核

`CombatWorld` 持有时间、实体表、意图队列、事件总线、效果管线，以及弹体 / AoE / 命中服务。一帧顺序固定：

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
- 玩家：季节 1/2 由 `PlayerCombatDriver` 消费输入缓冲走 Combo / 闪避；Arena 由 `BuffArenaPlayerComp` 消费同一个 `InputBufferComp`（单槽、窗口 `0.2s`）
- AI：感知写黑板，行为树只编排移动和 `PlaySkill`，不直接结算。约定 AI 不得 `Director.Stop`，树按 Actor 克隆
- Arena 敌人的树是**单个 `WanderShooter` 叶子**（游走 + 朝目标 + 定时施法），没有 selector / sequence；`BtFactory` 那套完整代码树服务于季节 1/2

### 效果与状态

效果包覆盖伤害、Tag、Buff、硬直、击退、击飞、击倒、无敌帧、Cue、生成弹体 / AoE / 召唤物。

- 属性：`Hp` / `MaxHp` / `Shield` / `Atk` / `Def` / `MoveSpeed` / `CritRate` / `DmgDealMul` / `DmgTakenMul`，Modifier 为 Add / Mul / Override
- Tag：可叠层计数，用于 Grounded、Cancel、Casting、Stunned、Invincible、Downed、Dead 等
- Buff：叠层、互斥、周期、驱散；Aura 用地板 occupancy 的 Enter / Exit 驱动

## 内容管线

两条路径，互不依赖：

**Arena（运行时，SO 是唯一真源）** —— `BuffArenaDatabaseAsset.Bake()` → `BuffArenaData`。内容不可用时**启动直接失败**（抛异常并带上 `LastContentError`），没有代码回退。资产在 `Assets/Combat/Config/Generated`：

| 目录 / 文件 | 内容 |
| --- | --- |
| `Content/Timelines/TL_*.asset` | 技能时间轴（Clip + Payload） |
| `Content/Projectiles/PD_*.asset` | 弹体定义（OnHit / OnExpire 效果以 sub-asset 挂载） |
| `Content/Aoes/AD_*.asset` | AoE 定义 |
| `Content/Skills/SK_*.asset` | 技能（输入 token / 时间轴 / 弹药消耗） |
| `Content/Actors/BA_*.asset` | 每个 Blueprint 的 Actor 数值 |
| `Content/BA_Motor.asset` / `Content/Cues.asset` | 运动策略 / Cue 表（Cue 的预制体绑定需要人工在 Inspector 维护） |
| `BuffArenaDatabase.asset` | 总入口（引用上面全部 + 运行时设置 + 相机取景） |

全部资产都在 Inspector 里手工维护：没有生成器，也没有「代码表 ↔ 资产」的比对。

**输入映射不进 SO**：按键写在 [BuffArenaSession.ApplyInput](Assets/Scripts/Combat/Unity/Game/BuffArenaSession.cs)，8 个动作按固定顺序压进单槽缓冲（后写覆盖先写，所以顺序即优先级）。它只依赖 `BuffArenaIds` 的 8 个 token，且每个 token 必须与对应技能资产上的 `InputToken` 相等——这是唯一需要和资产对齐的字符串约定。技能侧的其余字段（耗弹、时间轴、动画、传送弹开关、空弹回退 `FallbackSkillIdValue`）仍在 SO 上。

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
