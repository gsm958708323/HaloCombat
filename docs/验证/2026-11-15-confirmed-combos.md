# 2026-11-15 命中确认、Combo边表与真实输入验收

分支`codex/web-skill-editor-json`，上一重要节点`cd42976`。设计见ADR-0020。本批恢复通用命中确认语义、统一Combo输入与网页边表，完整历史13技能仍继续迁移。

## 已实现规则

只读检查本地Git历史fae7872的ComboComp、SkillDirector、DamageEffect、CombatControlTests和ComboTableAsset。ComboConfirm(1017)是来源当前施法对敌方存活实体的接触资格：先于无敌/护盾/零伤害检查取得，与真实HP伤害事件分离。资格校验完整Caster EntityId、Skill和CastId，只属于正在播放的当前Timeline；旧弹体、已结束/替换施法、友方和死亡目标不能授予。自然完成和全部Stop路径释放自己持有的TagLease，不清除外部同名Tag。

ComboTableDefinition仍是纯查表；ComboComp统一执行停止、死亡/眩晕/倒地/沉默与自动Cancel守卫，守卫失败保留确认。整表查找完成后无论匹配结果如何，消费自己持有的一次确认；后续能量/目标等SkillCast拒绝不恢复确认。输入只在成功施放后消费，失败保留到逻辑输入窗口过期。优先级最高的匹配边胜出；同输入、同优先级且前驱重叠的条目在JSON编译时拒绝，因为正向RequiredTags可能同时满足。

原Hitbox现在捕获开启时SkillOrigin，并传给物理接触ApplyEffectsIntent，修复旧路径丢失原始施法归属的问题。RuntimePlayerDriver和教学PlayerCombatDriver均通过ComboComp处理，后者也改为成功播放后才消费输入。Combo表/结果/组件从CombatComps按领域移到Combo.cs，未保留旧类型shim，文件均小于500行。

新增EvComboConfirmation与EvComboResolved，事件记录区分输入、资格授予/释放、匹配/未匹配；快照显示当前技能/CastId、自己持有的资格和待处理输入。静态表中的Key仅供真实匹配事件定位，不用显示名或数组索引当身份。

## 网页与JSON

“连招输入与接续”面板可编辑边ID、输入令牌、多个前置技能、Tag、命中确认勾选、优先级和接续技能，支持增删/设为起手；整表应用先严格校验，失败保留草稿，成功作为一次工程编辑可撤销。底层runtime JSON入口继续同源，不另存第二张表。校验期间有新编辑/改名/导入时拒绝旧结果覆盖，外部改表后需要显式读取当前表。

预览新增“试验输入令牌/发送输入”，使用真实InputBuffer/Combo/RuntimePlayerDriver/SkillCast。连续输入不重建世界；直接施放按钮仍是选中技能入口，编辑切换技能本身仍重建预览。发送input不绕过消耗、状态和取消窗；实例显示输入等待或消耗。

正式JSON追加ConfirmAttack示例：空闲→G1，G1→G2和G2→G1需1017；自动Cancel守卫在组件执行。原六技能Attack边表和Arena已有Fire1/4/5不更改绑定，ConfirmAttack用于验证通用能力，不能称为历史2001→2012→2013整套连段已还原。F6/F7技能闭包仍省略游戏角色/Combo，完整正式project.json保留；空表不会代码回退。

## 最终执行证据

| 检查 | 结果 |
| --- | --- |
| Core build/regress | 0错误，既有net6.0提示；ALL S1+S2 REGRESSION PASSED |
| CLI正式文件 | 11技能/10角色，g1 3伤害事件/22生效伤害/0.59999996位移，JSON RUNTIME PASSED |
| 宿主自检 | 真实ConfirmAttack输入、Hitbox原始归属、确认G1/G2与成功消费通过；此前全部自检通过 |
| 前端转换/构建 | 29/29；TypeScript/Vite通过，index-CG3HhEQn.js / index-DfNjODZv.css |
| Unity EditMode | job `48ceca61752346f1955fd62099234e18`，391/391 Passed，0失败/跳过 |
| Unity PlayMode | job `75d4642ce6d1409d836347f632e464d9`，53/53 Passed，0失败/跳过 |
| 同JSON跨宿主 | 六份完整快照，2147字段和值，0差异；结构/文本/顺序严格，浮点绝对1e-5/相对1e-6 |
| 实际正式Arena | Fire1临时绑定ConfirmAttack，优先级11取自网页发布；前置确认true→false，2次成功施放到Skill102，无待处理输入 |
| 最终Unity Error | 0 |

新增20项EditMode覆盖无敌/护盾/零伤害接触确认、友方/死亡/旧施法/完整Caster身份拒绝、所有结束路径/外部Lease隔离、查表成功失败均消费资格但不消费输入、取消窗/停止/沉默守卫保留资格、资源失败不恢复资格、真实Hitbox原始来源、优先级/冲突和同会话预览输入。PlayMode新增3项验证磁盘正式表的实际接招、坏表保留已有Arena、技能发布包缺少游戏表时不猜测回退。

## 浏览器真实编辑与发布

浏览器“打开正式内容”，展开confirmed-g1-g2边，把Tag改为bad-tag并应用，得到明确字段反馈，当前工程不被覆盖。改回1017，优先级10→11并应用，显示“连招表已应用，可撤销”。通过工程配置把预览target-1布置在x=.55，保留Caster攻击10；该布置只用于验收。

选择试验令牌ConfirmAttack，首次发送并推进10步：真实命中10点，G1/Cast1、1017×1、确认可用、输入无；再次发送并推进4步，世界原3帧顿帧后在第14步匹配confirmed-g1-g2、确认Lookup释放、原技能Replaced结束、G2/Cast2开始，资格与输入均无。没有点击直接G2施放或切换编辑选择来伪造连招。

实际保存并“发布正式内容”，磁盘回读表优先级11、RequiredTags=[1017]、目标x=.55和22条边。截图combo-browser/confirmed-transition.png同时显示真实输入/匹配事件与边表编辑控件。验收后恢复正式表优先级10、预览目标x2与原23技能工作工程/原F6/F7，临时测试工程归档到忽略证据目录，未进入Assets提交；浏览器显式重新载入磁盘、预览就绪，1920×1080临时尺寸撤销。

## 宿主与Unity同输入对照

使用真实网页发布文件browser-published.json。命中分支保留target=.55；未命中分支仅把独立测试副本target移到x8。分别首次input、推进10步、再次input、推进4步、再推进6步。Unity侧在PlayMode中执行同一SkillSimulation；真实Arena输入/组件路径另由PlayMode与手动场景验证。两类场景六份完整快照比较2147位置无差异。

| 场景/步 | 当前技能/Cast | 自有确认 | 缓冲输入 | 成功施放 | 生效伤害 |
| --- | --- | --- | --- | ---: | ---: |
| 命中10 | g1/1 | true | 无 | 1 | 10 |
| 命中14/20 | g2/2 | false | 无 | 2 | 10 |
| 未命中10 | g1/1 | false | 无 | 1 | 0 |
| 未命中14/20 | g1/1 | false | ConfirmAttack | 1 | 0 |

最后未命中输入仍处于0.2秒Actor逻辑窗口中，后续过期规则沿用原InputBuffer；没有在查表失败时直接删除它。

## 实际Arena与差异边界

MCP把网页发布JSON作为独立StreamingAssets临时内容，Fire1显式选择ConfirmAttack，训练目标设HP1000/x.55；起手后第11世界步（首步生成目标加10战斗步）资格true，再按Fire1并推进4步到15：Skill102、Cast2、确认false、InputBuffer空、目标HP950。目标扣50来自Arena正式玩家攻击，网页Caster攻击10，因此不把两种角色投影当完整快照同模型比较。临时内容已移到combo-browser/arena-project归档，正式StreamingAssets不保留测试副本。

原历史十三技能的2001/2012/2013 Timeline、共享Projectile定义、完整技能数值/ControlTags/ActionSpeed、移动AoE/弹体交互及原资源动画继续后续迁移。完整运行包导出、事件作者定位/输入记录复现及视频最终视觉/流程仍未完成。N04的边表、取消守卫、命中确认与失败消费语义已验收，不将其作为N02完整内容交付证明。

## 实施中修正

- 首轮断言假定stake为1000HP，而正式蓝图值为100；测试明确先设MaxHp再设Hp，避免Hp被旧MaxHp夹断，未改正式角色值。
- 第9步时0.02f×9小于JSON0.18f的实际浮点值，原Hitbox第10步开启；命中确认测试据实际运行取10步，保留原Timeline数值和执行行为，不为测试提前触发。
- 原ComboComp在解析时直接消费输入，Runtime驱动绕过组件直接查表；现已统一组件守卫/确认消费，成功施放后才消费输入，不保留双实现。

本机证据：Tools/SkillLab/.local/verification下combo-build.log、combo-regress.log、combo-host.log、combo-cli.log、combo-authoring.log、combo-web.log、unity-edit-combo.json、unity-play-combo.json和combo-browser实际发布/快照/comparison.json/arena-result.json/截图。提交保留源码/JSON/测试/ADR/本文，生成Unity工程与用户.gitignore改动继续留在工作树。
