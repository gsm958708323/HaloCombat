# SkillLab JSON 协议与运行边界

当前协议`formatVersion: 1`，文档字段写作camelCase；身份、枚举、parameters键和资源键按明确契约区分大小写。类型、有限数值、范围、重复字段、稳定身份、引用与图上下文由`Combat.Config/Json`校验。原视频未公开完整Schema，本协议属于HaloCombat，不宣称与原工具的隐藏格式互通。

## 按功能保存的磁盘工程

磁盘project.json是`formatVersion/id/name/files`索引。files.skills/projectiles/areas/buffs/timelines/summonBlueprints为按稳定ID命名的相对文件数组，files.preview/editor为独立文件；files.runtime保存motor/actors/combos/cues/views/arena等文件路径。SkillProjectFiles 组合为 SkillProjectDocument 后校验与编译。磁盘版节点坐标位于 `editor.json` 的 `nodePositions`，按集合、图和节点身份定位。运行时只读取玩法分片与试验布置。可携带作者工程在 `nodes[].position` 表示布局。

路径必须留在索引目录内，重复文件、未知字段、重复JSON键及坏模块拒绝并报文件。保存使用跨进程互斥、逐文件原子替换和失败回滚；ETag基于展开文档，单独改模块也使原ETag失效。示例、正式和用户工作工程均采用该结构，部署时携带索引引用闭包。以下单文件协议继续用于HTTP、下载、独立F6/F7和输入记录；磁盘索引不能脱离目录单独导入浏览器。

## 三种内容文件

| 文件 | 作用与结构 | 读取入口 |
| --- | --- | --- |
| 工程 | formatVersion/id/name/skills/preview，及可选buffs/summonBlueprints/timelines/projectiles/areas/runtime/editor；保存作者布局和草稿 | SkillProjectJson；持久化结构校验与完整运行编译分开 |
| 当前技能运行包 | formatVersion/revision/skillId/project；只包含实际依赖闭包，去除编辑布局 | SkillPublicationJson；校验原始载荷修订、依赖及字段后安装 |
| 完整运行内容 | 同工程协议，包含全部正式技能与runtime角色/规则/资源目录，省略editor/节点布局 | RuntimeProjectLoader；Arena/教学/CLI加载同文件 |

运行与作者工程从一个语义源派生，导出不重新生成内容身份。单技能包不能替代缺角色的完整Arena工程。Unity原生资源不编码进JSON，使用大小写敏感的Resources键引用，原Prefab/材质/动画随Unity源项目交付；加载时先验证资源。

## 图与定义

每个图持有稳定id，节点持有图内唯一id、kind、parameters，可选runtimeId/enabled/position；边具有稳定id、source/sourcePort/target/targetPort和显式order。数组位置、显示名、坐标不充当身份；重复入口仍执行多次。节点字段契约由`SkillEffectCompiler/SkillCastCompiler`及相应独立编译器维护，网页`model.ts`提供同字段的作者控件。类型化目录分别保存技能、Buff、蓝图、共享轴、弹体和区域，跨图引用使用明确字符串键。

target端口传目标数据；cast/execute/next和生命周期输出是执行连接，非法混接拒绝。即时效果图和跨类型依赖禁止递归；连发、引导、弹体连锁/分裂使用有界领域机制。连招图引用既有技能，循环由输入与时间轴窗口驱动，允许循环接续。普通传递七模式可以切换，共享定义根类型固定。

`pierce.parameters.maxHits`为非负整数，默认3，0无限；`chain.parameters`为count（默认3个额外目标）、radius（默认6米）、selection（nearest/random）。经hit及其next链执行，射线apply可传pierce。连锁必须有真实投射物命中上下文；每个接触的穿透优先，有限预算按连锁段重置。Core不再拥有弹体MaxHits/Chain分支；范围选取只由delivery负责，damage结算当前目标。Format-1旧行为输入、行内字段和范围伤害在协议层一次性转换；启用新旧策略混用拒绝，不双写。旧发布修订仍以转换前原始payload校验，用户槽位不自动重写。

连招运行规则为 runtime.combos 的 id/preSkills/input/requiredTags/priority/toSkill；可选 purpose 为 entry（空闲起手）、transition（动作接续）、cancel（公共取消），仅说明作者用途，不作为运行匹配过滤条件。缺失 purpose 的旧文件保持行为，非空前置在作者界面显示“未标用途”。多个前置是OR，多个正向所需Tag是AND；相同输入、重叠前置的平级规则拒绝。当前技能播放中还统一要求Cancel窗口。

editor.comboPositions 保存自动展开视图的 skillId/x/y（起手为 $start）；可选 editor.comboViews 保存 id/name/rootSkill/ruleIds/positions，每个视图引用同一运行规则，不复制内容、不隔离运行。视图规则只能引用已有非起手、非公共取消条目，身份和坐标在协议边界独立校验。完整工程保留视图，完整运行导出省略 editor，保留 purpose；单技能包继续省略完整角色和连招调度。

预览 Actor 的 comboDecision 与独立 comboDecisions 通道包含结构化诊断：输入序号、前置、规则、目标、实际执行技能、阶段、原因、窗口/命中资格、候选优先级和缺失Tag。成功路径只记录实际施放；相同输入结果去重且使用独立64条有界历史和序号，不挤占战斗事件；诊断从回放结果投影中排除，不要求开启输入录制。输入只在成功施放后消费；查表消费本次命中资格，同一次查找不回退其他候选，仍在缓冲期的输入可在后续逻辑步按新状态重求值。作者设计与使用见[连招编辑器设计与使用](连招编辑器设计与使用.md)。

cast.allowMovement允许动作中的自主移动；远程自动时序默认true，近战/冲刺/延迟位移默认false，显式时间轴含命中盒时默认false；BlockMove及动作控制Tag优先。弹体扫掠半径为弹体半径+目标Locomotion碰撞半径，墙/射程/去重保持原规则；射线宽度独立，不因本次弹体修正暗改。

默认单位：时间秒、空间米、速度米/秒、角度度、概率0..1、倍率无量纲；界面百分比换算不改变JSON。DTO整数拒绝小数取整、数字字符串、溢出与非有限值，旧内容中的精确整数`.0`身份继续合法；节点参数另按其字段类型校验。所有输入数值必须有限；运行中持续引导使用的无限时长在只读快照中表示null，不将Infinity写入JSON。时间轴、Buff、弹体与角色钟按各自契约推进，网页显示不决定战斗时间。

稳定对外字段的新增默认值、修订哈希与旧载荷按契约评估；内部过时路径直接删除。旧SO/Bake/cache不在正常加载或回退路径。完整DTO到领域映射失败时保留旧会话，不把新参数写入正在运行的旧弹体/轴/Buff。

`configure`命令携带完整候选project，完整编译后原子替换现有会话的后续定义、连招表、输入缓冲和Motor，保留时间、实体、位置、生命、冷却和资源。存量动作/弹体/区域/Buff保持捕获定义；分裂子弹沿用父定义，全局技能事件/Cue使用当前目录。节点编辑不重建世界；初始布置、种子、预览选项变更或显式重置仍重建。开启录制时配置更新进入同一输入日志。详见ADR-0046。

## 输入复现

输入文件包含formatVersion、stepSeconds=0.02、project、rootSkillId、按atStep排序的commands、endStep和expectedState。输入在同一步按记录顺序执行；配置/随机种子随文件保存，包含完整实体世代，不能只按索引绑定。上限30000步（600秒）/10000输入，超限不导出残缺记录。整数/身份必须一致，浮点末态比较容差0.0001；同Core版本重放不依赖当前磁盘工程，成功后才替换会话。修改运行代码后的跨版本录制结果可能被比较器拒绝，不承诺未来Core版本的结果永远相同。

网页会话默认不记录输入，通过会话创建选项显式开启；关闭时不保存初始记录配置，也不能导出不完整记录。网页无限弹药默认开启，只影响会话中的施法者；可选`preview.unlimitedAmmo`仅在输入文件里描述真实试验条件，未出现时为false，正式Arena不消费该字段。网页回放采用文件条件，恢复作者工程时不将此临时选项写入发布配置。UI显示∞，JSON与快照不写Infinity或伪造巨大弹药数。

2026-11-15按用户试射需求，unlimitedAmmo同时免除施法者能量消耗，冷却/状态/目标规则保留。录制的`auto-cast`命令带skillId时启动按固定步自动施放，省略skillId时停止，cancel亦停止；记录保存开关和瞄准输入，不为每个网络请求伪造一次独立开火。网页普通快照按事件Sequence增量读取，不传移动轨迹，诊断属性可降频；输入文件与Unity的完整快照契约继续独立使用。

## 保存、导入与发布

保存允许空工程/断线草稿，要求合法结构与保留引用；预览、配置应用和发布要求完整编译。ETag条件写入和原子替换防止覆盖新磁盘版本，保存仅确认请求时快照。导入原始文本先过C#，再预览替换/合并；合并重分配冲突身份并同步内部引用，可撤销。未知模块/枚举/字段不会静默变成空效果。

源工程宿主默认4192，仅监听127.0.0.1；F6/F7写`Assets/StreamingAssets/Combat`，Unity按F5明确加载。两槽共享工程/依赖版本不一致时保留旧会话，使用同步两槽更新。独立分发包默认保存在包内，可显式指定发布目录；正式内容发布仍写当前内容根的正式project.json。参见SkillLab使用说明、ADR及最终验收记录。
