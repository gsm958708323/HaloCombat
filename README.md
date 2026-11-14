# HaloCombat 纯 C# 战斗核（第一期总装）

## 网页技能编辑器（开发中）

在 `codex/web-skill-editor-json` 分支开发 JSON 网页技能编辑器。运行 `./Tools/SkillLab/Start.ps1` 启动本地网页，使用说明见 [Tools/SkillLab/README.md](Tools/SkillLab/README.md)，完整范围和验证记录见 [docs/实施进度.md](docs/实施进度.md)。

网页预览和 Unity `Assets/Scenes/HaloCombat/SkillLab.unity` 共用实际战斗核。网页发布 F6/F7 后，Unity SkillLab 运行中按 F5 重载，再按 F6/F7 施放。已接通瞬发、有限连发、引导、蓄力、目标/范围伤害、持续区域、近战、位移、射线、矩形障碍、召唤分身、共享 Buff 生命周期图、弹药换弹、追踪弹传送、可破坏物/木桶链爆，以及投射物散射/穿透/连锁/命中分裂/加减速/回旋/重力弹跳/墙反射，提供40个示例模板；发布包含技能/Buff/蓝图依赖闭包。现有V1–V4、训练场与Arena已接通同一JSON协议；历史技能/表现能力和最终参考对照仍在开发中。

## JSON 正式内容

运行源为 `Assets/StreamingAssets/Combat/project.json`，保存技能效果图、显式Timeline、Combo、角色蓝图、Motor、Cue资源键及Arena会话规则。网页“打开正式内容”读取它，右侧显式时间轴编辑取消/移动/命中盒/无敌区间及离散触发，“发布正式内容”完整校验后写回同一文件。Arena按F5重载正式工程及其F6/F7槽，V1–V4按F1重建、训练场重置后读取新内容。缺文件、坏引用或缺资源均报错，保留已有有效会话，不回退SO或代码。旧作者类型、31个SO资产和生成入口已删除；Config程序集不再依赖UnityEngine。

CLI用同一文件直接验证和运行：

```powershell
dotnet run --project Combat.csproj -- json Assets/StreamingAssets/Combat/project.json
dotnet run --project Combat.csproj -- json Assets/StreamingAssets/Combat/project.json homing
```

`CodeCombatContent`与独立Demo仍是教学测试样例，不是正式运行入口的后备。

`Assets/Scenes/Arena.unity`是可直接运行的JSON入口，也位于Build Settings首位。WASD/方向键移动，鼠标瞄准，按住左键/1重复主攻击；2/右键、3/中键、5/鼠标侧键、Space/Q/E走JSON的输入绑定。Esc暂停，HUD可重开/切换训练模式。F5明确重载，F6/F7使用同一正式玩家的消耗/冷却/状态守卫；槽必须从当前正式工程发布，其他工程的槽会报错并保留当前局。首次启动只读正式文件，避免未关联的旧调试槽阻止进入。

当前Arena复用已迁移技能和内置模型；Fire1弹药主攻击/缺弹换弹、Fire4追踪弹重施放和Fire5木桶自伤/死亡链爆已经接入JSON。历史13技能其余内容、目标独立顿帧/受伤反馈、原资源/动画和角色成长值仍需迁移，不能将本入口称为旧玩法/美术完整复刻。验收边界见[JSON Arena记录](docs/验证/2026-11-15-json-arena.md)、[弹药与重施放](docs/验证/2026-11-15-ammo-and-recasts.md)和[可破坏物验收](docs/验证/2026-11-15-constructs.md)。

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
