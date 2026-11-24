# 2026-11-15 角色独立顿帧与受伤反馈验收

分支`codex/web-skill-editor-json`，上一重要节点`73aadc9`。本批按ADR-0019补齐来源/目标独立顿帧与EvHurt等级反馈，关闭ENG-003；历史13技能其余内容和原资源/动画仍未全部迁移。

## 代码与协议

每个Actor持有ActorTime，包含自己的实际推进时间、pending/remaining帧数和当前停止标志。World在组件阶段之前统一锁存请求：同角色重复请求取最大值，来源/目标各自计数，命中当步不被新请求中途截断，下一步开始停止。冻结组件、Timeline/保持消耗、Buff、Health无敌时长、Motor与输入缓冲有效期；伤害/死亡/回收继续响应。停止角色仍可被别的弹体命中，不把顿帧当作无敌。

已有弹体/AoE不继承Owner的暂停；可以独立请求其自身顿帧。无关角色、木桶自伤和世界逻辑继续推进。世界级顿帧仍可显式请求，期间不消耗角色独立预算；最后停止步stopped=true时剩余可以为0，下一步恢复。Actor回收时重置自己的时间和预算，完整EntityId隔离新旧代。

伤害JSON新增sourceHitstopFrames、targetHitstopFrames、feedback；原hitstopFrames明确保留世界冻结语义。feedback是none/light/confirm/heavy/finisher表现等级，不硬编码SkillId隐式增加帧数。hurt-feedback效果节点在伤害链中复用当前DamageResult，默认仅实际扣血且目标仍存活时发EvHurt，致命一击只由死亡表现负责；它不切活动状态、不执行硬直，等级与帧数可独立编辑。

正式JSON：arena-ammo-shot显式Light/来源1/目标2，arena-tracked-shot基础反馈/来源0/目标2，arena-barrel Heavy/来源0/目标3；各伤害后连接独立hurt-feedback，木桶随后仍执行原点Cue。旧六技能教学内容保持原世界顿帧值，没有替换成新语义的兼容层。

ViewRegistry按完整Target EntityId路由EvHurt并随Router退订；Unity视图只有视觉闪白/短位移，不反写真实Transform。姿态脉动在自身停止时不推进，反馈用从事件发生时起的wall时间淡出。网页运行实例显示停止状态、剩余帧、角色时间、反馈等级，并用目标边框显示短时反馈/停止。示例由40增至41，新增“角色独立顿帧与反馈”。

## 最终验证

| 检查 | 结果 |
| --- | --- |
| Core build/regress | 0错误；ALL S1+S2 REGRESSION PASSED；既有net6.0提示保留 |
| 宿主自检 | 2/4独立停止、10实际扣血、Heavy事件和分离恢复；此前全部自检通过 |
| 前端authoring/build | 29/29，TypeScript/Vite通过，index-D1os21KT.js / index-D3x4xkwR.css |
| Unity EditMode | job `23ecf94f3e304bff808f6dff346048e7`，371/371 Passed，0失败/跳过 |
| Unity PlayMode | job `cda38b5a80db4cd78a599ef2a61e908e`，50/50 Passed，0失败/跳过 |
| 同发布包跨宿主 | 10份完整快照，4649字段和值，0差异；结构/文本/顺序严格，浮点绝对1e-5/相对1e-6 |
| 正式Arena实际木桶 | HP1000→992.5；目标停止3帧、来源/世界未停；Heavy反馈1次 |
| 最终Unity Error | 0 |

新增18项EditMode验证下一步锁存/max合并/来源等于目标、阶段一致性、停止时仍受伤与死亡、代际复用/请求失效、无关弹体/AoE/物体继续及自身停止、Buff/Health/能量/输入/冷却/保持消耗计时、Release、世界停止不吞角色预算、纯护盾/无敌/致命反馈边界、JSON坏帧数/等级、反馈路由完整身份/退订、等级wall淡出和复位。PlayMode新增3项覆盖真实正式普攻、目标视图闪白/姿态冻结、发布JSON快照/恢复、重开及旧代事件隔离。

## 网页实际编辑与发布

新建临时41示例工程，把“角色独立顿帧与反馈”来源帧数2→3、目标4→6，保留世界顿帧0和Heavy反馈。施放后逐帧：第1步扣10HP并排队；第2步caster/target-1停止且各自角色时间0.02秒，剩余2/5，target-2/3/4仍运行且时间0.04秒；第5步caster恢复、target-1仍停止/剩余2，无关目标时间0.10秒；第8步双方恢复。

实际从网页发布F6该技能、F7普通射击，文件skillId和3/6字段已回读验证。截图`actor-feedback-browser/stopped.png`显示两方停止、未受击目标运行、实际伤害和新字段。验收后恢复原23技能工作工程和原F6/F7，文件SHA256匹配备份；浏览器显式重新载入磁盘并确认预览就绪，临时1920×1080尺寸已撤销。

## 跨宿主对照

使用实际网页发布文件，独立宿主与Unity SkillLab分别运行：

1. F6单独施放，保存1/2/5/8/20步快照。
2. 先F7普通射击，20步后施放F6，保存21/22/25/28/40步快照。

| 场景/步 | 来源停止/剩余 | 目标停止/剩余 | 总实际伤害 | 说明 |
| --- | --- | --- | ---: | --- |
| 单独1 | false/3 | false/6 | 10 | 请求排队，当前步已完成 |
| 单独2 | true/2 | true/5 | 10 | 下一步分别锁存 |
| 单独5 | false/0 | true/2 | 10 | 攻击者先恢复 |
| 单独8 | false/0 | false/0 | 10 | 目标恢复 |
| 弹体21 | false/3 | false/6 | 10 | 原弹体x=-1.52 |
| 弹体22 | true/2 | true/5 | 10 | 原弹体继续到x=-1.28 |
| 弹体25 | false/0 | true/2 | 10 | 原弹体继续到x=-0.56 |
| 弹体28/40 | false/0 | false/0 | 90 | 既有弹体实际追加80扣血 |

十份快照完整比较4649位置，包含事件顺序、停止预算、每Actor时间、Buff/属性、身份/来源、坐标、反馈年龄、冷却与清理。比较和快照保存在忽略证据目录actor-feedback-browser。

## 正式Arena与反馈

MCP从Arena启动训练，把木桩HP设1000并移到x1.5以分辨7.5扣血；Fire5生成历史木桶后以桶来源引爆。第7步实测目标HP992.5、targetStopped=true/remaining2、sourceStopped=false、worldStopped=false、EvHurt1次、UnityView Feedback=Heavy/Sequence1。即目标3步停止已消费第1步，玩家Timeline和世界继续推进；真实状态保存在arena-barrel.json。原木桩1e9配置已保留，测试布置没有写回正式JSON。

PlayMode另实际验证普攻Light/来源1/目标2，目标环缩放在停止渲染帧保持，闪白随wall时间淡出。原Animator/模型/Cue资源及镜头等表现差异仍归N08，当前程序化反馈不声称像素复刻。

## 实施中修复与边界

- 首轮换弹回归仍预期41步已进入Reload；新增来源1帧后第41步动作刚结束、第42步驱动观察空闲并开始Reload，更新为精确断言并保持1.10秒换弹Payload验证。
- 首轮PlayMode49/50：刚发生的反馈被整个渲染长帧delta立即扣到0。修正为事件时钟起点，保留实际闪白>0、姿态冻结和淡出断言；最终50/50。
- ActorTime与输入使用同一实际逻辑时间，避免连续停止后输入窗口凭世界时间过期。全局停止的当前步标志也保持到该步结束，不能在最后停止步提前开放新技能。
- 反馈等级仅承载表现，不把全世界Damage HitIndex解释为技能内连段次数。历史ComboConfirm、ControlTags、ActionSpeed、全部旧13技能与资源仍需后续实现和对照。

ENG-003的角色独立逻辑冻结、JSON来源/目标字段、正式木桶目标3帧与EvHurt等级/视图链路已验收关闭。完整历史内容、全部参考界面/例子、运行包导出、事件作者定位/输入复现继续进行，本批不作为整体完成证明。

本机证据：Tools/SkillLab/.local/verification中的actor-feedback-build.log、actor-feedback-host.log、actor-feedback-regress.log、actor-feedback-authoring.log、actor-feedback-web.log、unity-edit-actor-feedback.json、unity-play-actor-feedback.json，actor-feedback-browser发布文件/快照/comparison.json/arena-barrel.json/stopped.png。提交保留源码、JSON、测试、ADR和本记录，生成工程文件/用户.gitignore继续保留在工作树。
