# 2026-11-15 JSON Arena直接入口验收

分支`codex/web-skill-editor-json`，上一重要节点`1b5de3e`。协议与生命周期决定见ADR-0016。本批完成可运行Arena会话与正式发布槽，完整编辑器/历史玩法目标仍进行中。

## 实现范围与依据

只读检查本地Git历史`fae7872`的BuffArenaSession/InputSource、Arena规则/表现/Actor资产，恢复10敌人上限、10秒补齐、5秒清尸、种子1与原输入优先顺序。历史代码/资产保存在Git历史中，不恢复SO或旧GameFlow管理器。

`runtime.arena`定义玩家/敌人/木桩引用、出生、矩形边界/障碍、规则、输入→Combo令牌、相机距离/高度/FOV/焦点高度。严格DTO→ArenaDefinition；纯C# ArenaSession组合CombatWorld/RuntimeActorFactory，处理一局输入、刷怪、训练、暂停、死亡与清尸。Unity负责采样、固定步钟、视图/HUD和相机，真实伤害仍在Core。场景已通过MCP生成/验证，Arena位于Build Settings首位，不含缺失脚本。

正式文件保留6技能/2Buff，新增3个Arena角色引用；角色目录含共享召唤蓝图共10项。玩家HP100000、Attack50、MoveSpeed3、Crit .05、Radius .25；木桩HP1e9、静止。敌人本批固定HP50/Attack15/MoveSpeed2.2，不声明已迁移历史随机成长/曲线。

WASD/方向键移动、鼠标瞄准；Fire1–5对应数字键与鼠标键，Space/Q/E对应Roll/Homing/Monkey。当前Fire4未绑定，等待真正传送弹能力。显式按下遵循Roll/Fire4/Monkey/Homing/Fire5/Fire3/Fire2/Fire1，同帧最多3项，零逻辑步不丢输入，成功才消费；主攻击保持独立，只在当前动作完成后重复，新的按下才尝试连招取消边。

HUD提供暂停/重开/训练切换/死亡提示，显示HP/能量/敌数/逻辑帧。暂停、失焦取消保持输入；死亡冻结战斗，独立wall time继续清尸，不增加World.Tick。训练只生成一个前方木桩，靠边时寻找内部净空；敌人以随机128次尝试寻找净空，本周期找不到时停止，等待下一周期。

## 发布与重载

首次启动、HUD重开/训练切换只读正式JSON；F5读取同目录F6/F7并重建。发布槽须来自同正式工程；Motor/Cue及两槽共享技能/Buff/蓝图版本冲突拒绝。合并后再次完整编译，F6/F7使用同一玩家SkillCastComp，消耗/冷却/状态/Cancel守卫一致。成功CastId拥有释放资格，失败槽或另一输入松开不能终止当前保持技能。

候选内容、World和隐藏表现根独立构建，表现先订阅，再Start设置玩家位置/属性并发Spawn。读取、编译或Start失败均销毁候选、保留旧局与待执行技能；成功后替换，退订旧事件、释放旧视图/材质/VFX、Shutdown旧World。测试实际构造了无法放置训练目标的有效配置，覆盖Start阶段失败，而非只测试坏文本。

工程合并保留现有Arena；首次导入Arena时同步重映射角色和Combo输入令牌，避免导入Fire1落入目的工程原Attack根。正式JSON和用户已有F6/F7均未被测试改写；磁盘发布测试使用独立临时目录并清理。

## 最终执行证据

| 检查 | 结果 |
| --- | --- |
| Unity MCP编译/场景检查 | Unity6000.0.32f1；Arena生成/验证通过，1个直接Bootstrap，无缺失脚本 |
| EditMode全量 | job `5251b3193b0c4f23bf3c959768a7a19b`，306/306 Passed，0失败/跳过；Arena27项、表现生命周期2项 |
| PlayMode全量 | job `03394fa6fa7c47d4bf656d5535004163`，41/41 Passed，0失败/跳过；包含新增Arena6项 |
| Core build/regress | 0错误，既有net6.0 EOL提示；ALL S1+S2 REGRESSION PASSED |
| CLI正式文件 | 6技能/10角色；g1 3伤害事件/22生效伤害/0.59999996位移；JSON RUNTIME PASSED |
| 宿主自检 | 原全量自检、Arena同种子120步输入重现、暂停及训练真实伤害通过 |
| 前端转换/构建 | 25/25；TypeScript/Vite通过，index-BB2J4Rbk.js、index-D3x4xkwR.css |
| 最终Unity Error日志 | 0 |

EditMode覆盖出生事件配置完成后才发布、首波/周期补齐、训练单目标、输入优先级/零步保留/保持与按下隔离、瞄准和移动独立且不越过击倒朝向锁、主攻击不截断自身延迟Payload、Release/Cancel差异、失败发布槽释放隔离、非有限输入、边界净空/移动、敌方弹体/区域不计敌数、暂停/死亡冻结、wall清尸、Dispose、无效角色/绑定/数值/出生、同项目/版本冲突及真实资源守卫；另外验证旧代死亡/回收事件不影响新视图，以及Hitstop退订不叠加。

PlayMode验证真实场景移动/瞄准/Combo伤害和视图、坏JSON/Start失败保留旧局、真实磁盘F6/F7成本/守卫/项目冲突、重开释放旧Actor/视图/VFX/事件、暂停死亡清尸/重开。表现断言保留模型脚环厚度和能量核尺寸。

## 实际场景与宿主对照

MCP从Arena场景实际进入Play Mode，关闭自动推进、重开并推进80固定步：活敌10、累计生成10，加载错误null；画面可见蓝色玩家、红色敌人、实际飞行弹体和HUD。相机使用JSON的距离2.5/高度2.5/FOV60；新增focusHeight .75适配当前1.8米内置模型，保证完整玩家可见，不冒称原美术视觉已还原。最终画面保存在`.local/verification/arena-combat-1.png`。

随后通过真实ArenaBootstrap切换训练，按住主攻击90步、松开后再推进30步；独立宿主用同正式JSON、同ArenaSession和输入重放：

| 投影 | Unity实际Arena | 独立C#宿主 |
| --- | --- | --- |
| 固定步 | 120 | 120 |
| 生效伤害 | 220 | 220 |
| 伤害事件 | 5 | 5 |
| 成功施放 | 4 | 4 |
| 累计刷敌 | 0 | 0 |
| 存活实体 | 2 | 2 |

该对照证明同一实际会话的跨宿主执行；本批未重新做浏览器完整快照对照，前批网页/Unity SkillLab及正式输入对照有独立记录。

## 实施中发现并修正

- 首轮PlayMode40/41：保持主攻击每步尝试Combo取消，G1延迟弹体被截断导致伤害0。修正保持/新按下的区别，新增独立回归，保留真实伤害断言。
- 实际80/120步画面检查发现敌方弹体参与敌人数统计，改为具有Health的敌方角色并排除召唤；补充弹体/区域回归。
- 实际画面发现既有呼吸动画把脚环/能量核局部缩放覆盖为单位向量，造成巨大柱体/球。修正为乘原始模型尺寸，删除重复区域脉动组件；对实际角色尺寸增加断言，并区分敌方颜色。
- 失败候选Start原本可能先销毁旧局；现以独立隐藏表现根准备新会话，成功后替换。输入释放以成功CastId关联，避免失败按键中断另一个保持技能；持续瞄准遵守Motor朝向锁和Steer策略，仅开始动作使用显式Snap。

## 后续边界

历史13技能、Ammo/缺弹换弹回退、传送弹二段、木桶链爆、角色随机/成长曲线、ControlTags/动作速度/原动画资源仍未完成；当前Arena只是已迁移6技能的正式入口。完整运行包/内容包导出、作者节点定位/输入复现及视频1:1视觉交互与全部例子仍需验收。

本机原始日志/快照位于Git忽略目录`Tools/SkillLab/.local/verification`：unity-edit-arena.json、unity-play-arena.json、arena-core-regress.log、arena-host-checks.log、arena-cli.log、arena-authoring-tests.log、arena-web-build.log、arena-manual.json、arena-combat-1.png。提交保留源码、测试、场景和本记录，不包含生成的Unity csproj、个人.gitignore改动或本机证据产物。
