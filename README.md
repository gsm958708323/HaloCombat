# HaloCombat 纯 C# 战斗核（第一期总装）

## 运行

dotnet run
dotnet run -- tag
dotnet run -- attr
dotnet run -- buff
dotnet run -- motor
dotnet run -- clip
dotnet run -- melee
dotnet run -- proj
dotnet run -- season
dotnet run --project Combat.csproj -- knock
dotnet run --project Combat.csproj -- dodge
dotnet run --project Combat.csproj -- aura
dotnet run --project Combat.csproj -- bt
dotnet run --project Combat.csproj -- perc
dotnet run --project Combat.csproj -- enemy
dotnet run --project Combat.csproj -- summon
dotnet run --project Combat.csproj -- season2
dotnet run --project Combat.csproj -- lesson
dotnet run --project Combat.csproj -- training-camp
dotnet run --project Combat.csproj -- regress

默认 `season` = 第一期验收：G1 近战+刀光 Cue+火球灼烧、G2 火地叠 3、受击停轴、Bake 清缓存、死亡清弹圈。

## 冻结

Actor / Comp / World / Time / EventBus / IntentQueue / Pool
Combo 边表 + Play(skill, timeline)
InputBuffer 单槽 0.2s

## 唯一结算

IEffect.Apply(ref EffectContext) + World.Deliver
位姿：Request* → Locomotion.Integrate
血量：AttributeSet.SetBase(Hp)

## Unity

运行时源码位于 `Assets/Scripts/Combat/Core`。第一、二季共 16 个 Demo 都有对应 Unity
验证场景，位于 `Assets/Scenes/HaloCombat`；场景中的 `HaloCombatDemoRunner` 会在 Play Mode
启动时运行对应 Demo，并把结果写入 Unity Console。第二季场景仍是逻辑验证场景，不包含
SO、Bake 或可操作表现层关卡。

## 日志

共享代码使用 `Combat.Core.CombatLog` 输出同步过程日志。日志格式为
`[等级][category] message`，默认 `MinimumLevel` 为 `Debug`；将其设置为 `Info`、`Warn`
或 `Error` 可以减少过程输出。

`.NET` 入口会注册 `ConsoleLogSink`，Unity 的 `HaloCombatDemoRunner` 会注册
`UnityLogSink`，因此 Demo 调用点不需要依赖具体平台。日志 Sink 自身发生异常时会被忽略，
不会中断战斗流程。

Demo 使用 `TagInput`、`Attribute`、`Buff`、`ActivityMotor`、`ClipPayload`、`MeleeDamage`、
`ProjectileAoe`、`SeasonOne`、`Knockdown`、`DodgeHitstop`、`AuraHoming`、`BehaviorTree`、
`Perception`、`EnemyAi`、`Summon` 和 `SeasonTwo` 作为 category。`CombatLog.SetCategoryFilter("TagInput")`
会立即只保留该 category 的日志；传入 `null`、空字符串或 `All` 会恢复全部输出。Unity 中可在
`HaloCombatDemoRunner` 的 `Category Filter` 下拉框选择筛选条件，Play Mode 修改会立即生效。

`.NET` 命令行可传入 `--category TagInput` 或 `--category=TagInput`，例如：

```powershell
dotnet run --project Combat.csproj -- --category TagInput tag
```

Unity Console 会为 category 添加颜色；`.NET` Console 保持纯文本，便于重定向和解析。

## V1-V4 ARPG 教学演示

`V1Motor`、`V2Melee`、`V3ProjectileAoe` 和 `V4AiSummon` 现在是四个可操作的
ARPG 教学场景。场景会自动播放由 `CombatLesson` 驱动的确定性演示，并用 HUD 展示
当前步骤、Actor 状态、事件流和技能时间轴。`CombatLesson` 位于纯 C# Presentation 层，
因此可通过 `dotnet run --project Combat.csproj -- lesson` 在不启动 Unity 的情况下回归
四个场景的核心行为。

运行时控制：`Space` 暂停/继续，`N` 单步，`R` 重播，`F5` 显示底层调试信息。
Unity 表现层使用程序化的玩家、敌人、目标桩、召唤物、Projectile 和 AoE 视觉 Profile，
不承担伤害或状态结算。

也可在 Unity 菜单使用 `Tools/HaloCombat/Create Demo Scenes` 重建场景，或使用
`Tools/HaloCombat/Verify Demo Scenes` 在编辑器中验证所有场景配置和 Demo。

## TrainingCamp 功能验证沙盒

`Assets/Scenes/HaloCombat/TrainingCamp.unity` 是独立的手动验证场景，不改变上述教学
Demo。场景默认生成玩家和无限生命 Passive Dummy；按 `Q` 可切换 AI Dummy。面板按钮与
快捷键覆盖移动、跳跃、攻击、Dodge、Buff/Dispel、Fireball、Homing、Fire Ground、Aura、
Summon、Knockdown、清理和检查。快捷键：`1-8` 执行对应运行时操作，`R` 重置，`Tab`
隐藏面板，`F1` 清理运行时对象，`F2/F3` 执行当前/全量检查。

纯 C# 验证不依赖 Unity Scene 或 RunnerTest：

```powershell
dotnet run --project Combat.csproj -- training-camp
```

场景可通过 `Combat/Create Training Camp Sandbox` 重建，并通过
`Combat/Verify Training Camp Sandbox` 检查入口、无限血木桩和初始运行时契约。
