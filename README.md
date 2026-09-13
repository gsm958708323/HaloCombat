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
- 血量：只通过 `AttributeSet.SetBase(Hp)` 修改

## 分层（表现层直接写unity相关逻辑）

| 模块 | 职责 |
| --- | --- |
| `Combat.Core` | 纯 C# 战斗核。实体、时间、效果、技能、弹体、AoE、Buff、行为树 |
| `Combat.Config` | Unity SO 配置，`Bake()` 成运行时定义 |
| `Combat.Game` | 对局会话、50Hz 逻辑步、暂停、胜负、输入路由 |
| `Combat.Presentation` | `PresentHub → PresentActor → PresentComp` 视图组件 |
| `Combat.Unity` | 场景入口、Input System、HUD、VFX / Floater 池 |
| `Combat.Demos` | 纯 C# 逻辑 Demo，Editor-only |
| `Combat.Editor` | 数据库生成、场景检查、Demo 校验 |

`Core` / `Config` / `Unity` / `Demos` / `Editor` 是独立 asmdef。`Game` 与 `Presentation` 编进 `Combat.Unity`。

源码位置：

- 逻辑核：`Assets/Scripts/Combat/Core`
- 配置：`Assets/Scripts/Combat/Config`
- 对局 / 表现 / Unity 胶水：`Assets/Scripts/Combat/Game`、`Presentation`、`Unity`
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

Hitstop 只推进 wall time，暂停逻辑帧。实体用 `EntityId(index, generation)`，Blueprint 由 `FighterActorFactory` 组装玩家、敌人、木桩、召唤物、弹体和 AoE。

### 技能与活动

- Timeline Clip：`CancelTag` / `Move` / `Hitbox` / `IFrame`
- Timeline Payload：Cue、生成弹体 / AoE / 召唤物
- 活动机：`Root` / `Attack` / `Hit` / `Knockdown` / `Dead`，各自带位移与朝向策略
- 玩家：`PlayerCombatDriver` 消费输入缓冲，走 Combo 或闪避技能
- AI：感知写黑板，行为树只编排移动和 `PlaySkill`，不直接结算。约定 AI 不得 `Director.Stop`，树按 Actor 克隆

### 效果与状态

效果包覆盖伤害、Tag、Buff、硬直、击退、击飞、击倒、无敌帧、Cue、生成弹体 / AoE / 召唤物。

- 属性：`Hp` / `MaxHp` / `Shield` / `Atk` / `Def` / `MoveSpeed` / `CritRate` / `DmgDealMul` / `DmgTakenMul`，Modifier 为 Add / Mul / Override
- Tag：可叠层计数，用于 Grounded、Cancel、Casting、Stunned、Invincible、Downed、Dead 等
- Buff：叠层、互斥、周期、驱散；Aura 用地板 occupancy 的 Enter / Exit 驱动

## 内容管线

Unity 侧 `CombatDatabaseAsset.BakeAll()` 把技能、角色、时间轴、弹体、AoE、召唤物、Cue、Motor 烘焙成 `BakedCombatData`。Demo 走 `CodeCombatContent`，内嵌第一期默认表，不依赖 SO。

已覆盖内容包括近战连段、火球灼烧、火地叠层、闪避无敌帧、追踪弹、光环减速、近战 / 远程 / 守卫 AI 和召唤物。

## 表现与对局

`LogicTicker` 以 `1/50s` 固定步长推进逻辑，渲染用 remainder 插值。`GameFlow` 状态为 Title → Loading → Arena → Result。

正式 Arena 由 `ArenaBootstrap` 创建 `CombatWorld`、`PresentHub` 和 `ArenaSession`。表现层订阅 `EvEntitySpawn`、`EvCue`、`EvDamage`、`EvImmune`、`EvHeal`、`EvHitstop`，用对象池播放特效和飘字。

逻辑验证场景不包含可操作表现层。第二季 16 个 Demo 场景仍是纯逻辑验收，不跑 SO Bake 或 Arena 操作关。

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

默认 `season` 验收：G1 近战 + 刀光 Cue + 火球灼烧、G2 火地叠 3、受击停轴、Bake 清缓存、死亡清弹圈。

Unity Editor 中对应场景在 `Assets/Scenes/HaloCombat`。Play Mode 下 `HaloCombatDemoRunner` 会跑同一套 Demo，并把结果写到 Console。正式对局场景是 `Assets/Scenes/Arena.unity`。

`FinalVerification.Run` 会生成默认数据库、检查场景，并校验全部 Demo。

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
