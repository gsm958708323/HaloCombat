# HaloCombat：JSON 技能编辑器与战斗核

## 网页技能编辑器

JSON 网页技能编辑器位于 `codex/web-skill-editor-json` 分支。源码版双击根目录 [StartSkillLab.bat](StartSkillLab.bat)，自动构建、启动并打开网页（需要 Node.js 22 与 .NET SDK 10）；也可运行 `./Tools/SkillLab/Start.ps1`。Windows独立包解压后双击`Run.cmd`，无需安装Node/.NET即可试玩。使用说明见 [Tools/SkillLab/README.md](Tools/SkillLab/README.md)，完整范围和证据见 [docs/最终验收.md](docs/最终验收.md)。

网页预览和 Unity `Assets/Scenes/SkillLab.unity` 共用实际战斗核。网页发布到槽位 1–4 后，运行中的 Unity SkillLab 会自动加载。用左键、Q、E、R 或小键盘 1–4 施放。已接通瞬发、有限连发、引导、蓄力、目标/范围、近战、位移、射线、召唤、Buff、换弹、传送弹、木桶链爆、完整弹体行为和飞行触发，提供53个模板。工程、当前技能闭包、完整运行内容三种JSON可导出回读；事件支持作者节点定位与输入文件重放。最终Unity EditMode796/796、PlayMode100/100、前端100/100通过；54份模板记录在Unity对照94917值匹配。

## JSON 正式内容

运行入口为 `Assets/StreamingAssets/Combat/project.json`索引，技能、弹体、区域、Buff、时间轴、召唤蓝图及runtime角色/连招/Motor/Cue/View/Arena按功能保存为独立JSON。网页 Arena 工作区和Unity经同一SkillProjectFiles组合并校验；“发布正式内容”写入索引及引用模块。工具栏“技能设置”编辑显式时间轴，右侧仅选中节点属性。Arena按F5重载正式工程及其F6/F7槽。缺文件、坏引用或缺资源保留有效会话，不回退SO或代码；Config程序集不依赖UnityEngine。

射击支持“施放时允许移动”；普通远程默认开启，近战/冲刺与显式控制限制保留。弹体按自身及目标碰撞半径扫掠，避免视觉擦过目标却无命中。预览取消轨迹、瞄准不触发整场重绘，传输与显示刷新分开；后续分片/碰撞/性能验证见ADR-0045。

CLI用同一文件直接验证和运行：

```powershell
dotnet run --project Combat.csproj -- json Assets/StreamingAssets/Combat/project.json
dotnet run --project Combat.csproj -- json Assets/StreamingAssets/Combat/project.json homing
```

`CodeCombatContent`与独立Demo仍是教学测试样例，不是正式运行入口的后备。

网页“连招输入与接续”可编辑正式Combo表，并在同一预览发送实际令牌。命中确认按完整Skill/CastId绑定，取消窗与施放守卫生效，失败输入保留；原2001→2012→2013确认链已纳入JSON，见[历史对照](docs/验证/2026-11-15-original-arena-contract.md)。

显式时间轴支持六种动作控制与动作速度缩放，角色/召唤/Buff的ActionSpeed只影响明确开启缩放的动作轴。控制通过独立租约释放，角色钟/冷却/Buff/已发弹体保持独立；受控位移射击示例在2倍速度下仍完成2米位移，见[时间轴控制验收](docs/验证/2026-11-15-timeline-controls.md)。

正式Arena的男女原角色、枪械和CharacterAnimator已从历史资源恢复。`runtime.views`具名目录与`timeline.animation`连接网页JSON和真实Animator，动作相位跟随Timeline、循环/受击跟随ActorTime，支持独立顿帧、同技能重播和重开；缺失资源/状态拒绝替换旧局。编辑方式见[角色动画配置](docs/角色动画配置.md)，实际浏览器发布和Unity证据见[角色动画验收](docs/验证/2026-11-15-actor-animation.md)。

持续区域可配置前移、同队弹体吸收、水平冲量和半径增长，吸收端口与实时半径/速度/次数已接入网页。Arena的Monkey输入使用原SK2003/TL3003/AD4102；弹体可显式配置同目标间隔重击和无敌穿透，回旋往返两次命中已验收。吸收沿用逻辑步端点重叠，不是高速弹体连续拦截，详见[移动区域与重复接触验收](docs/验证/2026-11-15-area-interactions.md)。

`project.timelines`独立动作图可被多个技能以`timelineRef`复用，网页支持提取/编辑/复制/引用检查与发布闭包。正式2001→2012→2013确认连段已接通，二、三段共用TL3013/PD4012但保持独立耗弹与命中反馈；命中施加原4秒易伤。实际验证见[共享时间轴与原确认连段](docs/验证/2026-11-15-shared-timelines.md)。

`Assets/Scenes/Arena.unity`是可直接运行的JSON入口，也位于Build Settings首位。WASD/方向键移动，鼠标瞄准，按住左键/1重复主攻击；2/右键、3/中键、5/鼠标侧键、Space/Q/E走JSON的输入绑定。Esc暂停，HUD可重开/切换训练模式。F5明确重载，F6/F7使用同一正式玩家的消耗/冷却/状态守卫；槽必须从当前正式工程发布，其他工程的槽会报错并保留当前局。首次启动只读正式文件，避免未关联的旧调试槽阻止进入。

当前Arena包含原13技能ID、缺弹换弹/传送弹/木桶链爆、三段确认、双相位Motor、独立顿帧、原角色和实体/Cue资源，以及原游走/生成随机与累计成长。历史技能794值、环境84值及实际行为证据见`docs/验证`；统一文件流程和分发结果见最终验收。原提交没有音频文件，不能宣称恢复了原音效。

## 运行

```powershell
dotnet run --project Combat.csproj
dotnet run --project Combat.csproj -- tag
dotnet run --project Combat.csproj -- attr
dotnet run --project Combat.csproj -- buff
dotnet run --project Combat.csproj -- motor
dotnet run --project Combat.csproj -- clip
dotnet run --project Combat.csproj -- melee
dotnet run --project Combat.csproj -- proj
dotnet run --project Combat.csproj -- season
dotnet run --project Combat.csproj -- knock
dotnet run --project Combat.csproj -- dodge
dotnet run --project Combat.csproj -- aura
dotnet run --project Combat.csproj -- bt
dotnet run --project Combat.csproj -- perc
dotnet run --project Combat.csproj -- enemy
dotnet run --project Combat.csproj -- summon
dotnet run --project Combat.csproj -- season2
dotnet run --project Combat.csproj -- all
```

默认 `season` 演示 G1 近战、刀光和火球灼烧、G2 火地叠层、受击停轴与死亡清理。`all` 按 Unity 教学场景 01–16 的顺序运行，每个 Demo 开始前恢复 G1 默认配置，全部完成后输出 `ALL 16 TEACHING DEMOS COMPLETED`。

## 冻结

Actor / Comp / World / Time / EventBus / IntentQueue / Pool
Combo 边表 + Play(skill, timeline)
InputBuffer容量3的FIFO，独立输入年龄；窗口由角色配置，正式Arena为0.8秒，教学默认0.2秒。

## 唯一结算

IEffect.Apply(ref EffectContext) + World.Deliver
位姿：Request* → Locomotion.IntegrateBeforeHitDetection（自主/技能位移）→ 命中结算 → IntegrateAfterHitDetection（受击位移/重力）。Timeline到期判定和控制在接触结算后收尾，最后有效步不会漏判。
血量：AttributeSet.SetBase(Hp)

## 如何拓展技能

新增技能按“拆解需求 → 组合已有能力 → 补齐缺失能力 → 验证”的顺序推进。优先使用效果系统组合效果节点，缺少触发时机时补充对应事件，作为效果链的执行入口。

### 1. 拆解技能需求，确定职责

先明确触发时机、执行操作、上下文和需要保存的状态，再查已有节点、定义、代码与测试。

| 需求 | 对应职责 | 例子 |
| --- | --- | --- |
| 什么时候执行 | 触发事件或生命周期入口 | 投射物命中、Buff 周期、技能自然完成 |
| 执行什么操作 | 效果节点 | 伤害、治疗、施加 Buff、生成区域、播放 Cue |
| 对谁执行、在哪里执行 | EffectContext | Source、Target、Point、Dir、Power、SkillOrigin、DamageResult |
| 持续多久、保存哪些状态 | Buff、Timeline、组件或独立运行实例 | Buff 年龄、动作区间、每颗弹体的命中记录 |

### 2. 已有能力能够表达时，直接组合配置

在网页中新增或复制技能，设置目标、施放与时间轴，再连接效果节点并引用已有的弹体、区域和 Buff 定义。技能差异通过 JSON 中的参数、连线和定义引用表达。

以“命中附加灼烧，灼烧结束后爆炸”为例，组合方式如下：

```text
投射物命中   → 伤害 → 施加灼烧 Buff
Buff 周期    → 对持有者造成伤害
Buff 自然到期 → 以持有者位置做范围伤害 → 爆炸 Cue
```

若要求实际扣血才附加灼烧，将施加 Buff 接到“扣血后条件”的 apply 端口。普通 next 连线只保证执行顺序，条件应通过明确的条件节点表达。

工程读取与保存统一走 SkillProjectFiles：用户工作工程位于 `Tools/SkillLab/.local/arena/project.json` 及其引用模块，正式内容从 `Assets/StreamingAssets/Combat/project.json` 索引加载。具体作者操作见 [技能工作台使用说明](Tools/SkillLab/README.md)。

### 3. 效果能力已有、缺少执行时机时，补触发入口

例如，假设要实现“护盾破裂时击退周围敌人”，且范围查询和击退已能复用，就在护盾系统确认破裂的位置增加事件入口，再连接范围查询与击退效果链。

新增入口时明确触发条件、次数、来源/目标/位置语义，以及自然结束、打断和清理的差别。携带完整实体与原始施法身份，遵守既有递归抑制规则，并在协议校验、转换和网页端口中接通。事件可以是生命周期回调或作者图输出端口，按已有边界接入。

可参考 [投射物命中投递](Assets/Scripts/Combat/Core/ProjectileService.cs)、[Buff 生命周期入口](Assets/Scripts/Combat/Core/BuffDefinition.cs)和[技能事件路由](Assets/Scripts/Combat/Core/SkillEvents.cs)。这些入口都应将效果交给统一执行管线：

```text
命中 / 生命周期 / 技能事件
    → 对应效果包 + EffectContext
    → World.Deliver → EffectPipeline → IEffect.Apply
```

### 4. 基础操作缺失时，补通用效果节点

在 Combat.Core 中实现职责明确、可复用的 IEffect，再补齐 JSON 字段、参数与端口校验、独立转换、网页作者控件和相关验证。命名围绕实际能力，新增技能继续组合这些能力。

接口见 [Pipeline.cs](Assets/Scripts/Combat/Core/Pipeline.cs)，节点转换参考 [SkillEffectCompiler.cs](Assets/Scripts/Combat/Config/Json/SkillEffectCompiler.cs)，连接和上下文校验参考 [EffectGraphIndex.cs](Assets/Scripts/Combat/Config/Json/EffectGraphIndex.cs)。作者图由 Combat.Config/Json 转换成领域定义与有序效果包，Core 执行领域能力；表现资源绑定与播放归 Combat.Presentation/Combat.Unity。

持续行为先确定状态归属：动作区间由 Timeline 推进，持续属性和周期效果由 Buff 管理，独立寿命对象由运行实例管理。效果节点负责发起操作，计时、命中记录等可变状态保存在对应实例中。技能资源、冷却与状态守卫通过统一施放入口处理。

### 5. 按改动范围完成验证

使用独立临时工程验证正常触发、无效目标、打断/清理、重复触发及必要的嵌套组合，保留用户工作工程和发布槽数据。根据改动范围执行相关检查：

- 教学样例使用 `dotnet run --project Combat.csproj -- all`。
- 正式内容使用 `dotnet run --project Combat.csproj -- json Assets/StreamingAssets/Combat/project.json` 校验并试放。
- 网页在 `Tools/SkillLab/Web` 执行 `npm run build`，再通过独立临时工程验证实际作者操作、预览、发布与回放。
- JSON 或输入链路使用实际文件验证网页与 Unity。通过 Coplay Unity MCP 检查编译，在实际场景中进行 Play Mode 操作，并检查 Error 日志。

非显然的设计决策、限制与风险写入 ADR，验收证据记录到 `docs/验证`。完成相关验证后按工程约定本地提交。

## Unity

运行时源码位于 `Assets/Scripts/Combat/Core`。第一、二季共 16 个 Demo 都有对应 Unity
验证场景，位于 `Assets/Scenes/HaloCombat`；场景中的 `HaloCombatDemoRunner` 会在 Play Mode
启动时运行对应 Demo，并把结果写入 Unity Console。第二季场景仍是逻辑验证场景，不包含
SO、Bake 或可操作表现层关卡。
在 Unity 中选择 `Tools/HaloCombat/运行全部教学 Demo`，可按 01–16 顺序批量运行。

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

## 游戏与测试场景

在 Unity 菜单 `Combat → 打开场景` 选择「Arena 正式」「Arena 木桩」或「SkillLab」。Arena 用 `F5` 重载正式内容和发布槽，空格翻滚、`F` 放木桶、`C` 追踪射击、`X` 放吸收球。

打开 SkillLab 后点 Play，试验场会自动加载技能。网页重新发布后也会自动应用，可按 `F5` 手动重载，按 `.` 单步推进。两种场景均用 WASD 移动、鼠标瞄准、左键/Q/E/R 或小键盘 1–4 施放、`Esc` 暂停。
