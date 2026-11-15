# HaloCombat：JSON 技能编辑器与战斗核

## 网页技能编辑器

JSON 网页技能编辑器位于 `codex/web-skill-editor-json` 分支。运行 `./Tools/SkillLab/Start.ps1` 启动本地网页，或解压Windows独立包双击`Run.cmd`，无需安装Node/.NET即可试玩。使用说明见 [Tools/SkillLab/README.md](Tools/SkillLab/README.md)，完整范围和证据见 [docs/最终验收.md](docs/最终验收.md)。

网页预览和 Unity `Assets/Scenes/HaloCombat/SkillLab.unity` 共用实际战斗核。网页发布F6/F7后，Unity运行中按F5重载，再按F6/F7施放。已接通瞬发、有限连发、引导、蓄力、目标/范围、近战、位移、射线、召唤、Buff、换弹、传送弹、木桶链爆、完整弹体行为和飞行触发，提供53个模板。工程、当前技能闭包、完整运行内容三种JSON可导出回读；事件支持作者节点定位与输入文件重放。最终Unity EditMode796/796、PlayMode100/100、前端100/100通过；54份模板记录在Unity对照94917值匹配。

## JSON 正式内容

运行入口为 `Assets/StreamingAssets/Combat/project.json`索引，技能、弹体、区域、Buff、时间轴、召唤蓝图及runtime角色/连招/Motor/Cue/View/Arena按功能保存为独立JSON。网页“打开正式内容”和Unity经同一SkillProjectFiles组合并校验；“发布正式内容”写入索引及引用模块。工具栏“技能设置”编辑显式时间轴，右侧仅选中节点属性。Arena按F5重载正式工程及其F6/F7槽，V1–V4按F1重建、训练场重置后读取。缺文件、坏引用或缺资源保留有效会话，不回退SO或代码；Config程序集不依赖UnityEngine。

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

默认`season`验证G1近战/刀光/火球灼烧、G2火地叠层、受击停轴和死亡清理。

## 冻结

Actor / Comp / World / Time / EventBus / IntentQueue / Pool
Combo 边表 + Play(skill, timeline)
InputBuffer容量3的FIFO，独立输入年龄；窗口由角色配置，正式Arena为0.8秒，教学默认0.2秒。

## 唯一结算

IEffect.Apply(ref EffectContext) + World.Deliver
位姿：Request* → Locomotion.IntegrateBeforeHitDetection（自主/技能位移）→ 命中结算 → IntegrateAfterHitDetection（受击位移/重力）。Timeline到期判定和控制在接触结算后收尾，最后有效步不会漏判。
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
