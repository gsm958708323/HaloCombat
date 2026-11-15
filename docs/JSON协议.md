# SkillLab JSON 协议与运行边界

当前协议`formatVersion: 1`，文档字段写作camelCase；身份、枚举、parameters键和资源键按明确契约区分大小写。类型、有限数值、范围、重复字段、稳定身份、引用与图上下文由`Combat.Config/Json`校验。原视频未公开完整Schema，本协议属于HaloCombat，不宣称与原工具的隐藏格式互通。

## 三种内容文件

| 文件 | 作用与结构 | 读取入口 |
| --- | --- | --- |
| 工程 | formatVersion/id/name/skills/preview，及可选buffs/summonBlueprints/timelines/projectiles/areas/runtime/editor；保存作者布局和草稿 | SkillProjectJson；持久化结构校验与完整运行编译分开 |
| 当前技能运行包 | formatVersion/revision/skillId/project；只包含实际依赖闭包，去除编辑布局 | SkillPublicationJson；校验原始载荷修订、依赖及字段后安装 |
| 完整运行内容 | 同工程协议，包含全部正式技能与runtime角色/规则/资源目录，省略editor/节点布局 | RuntimeProjectLoader；Arena/教学/CLI加载同文件 |

运行与作者工程从一个语义源派生，导出不重新生成内容身份。单技能包不能替代缺角色的完整Arena工程。Unity原生资源不编码进JSON，使用大小写敏感的Resources键引用，原Prefab/材质/动画随Unity源项目交付；加载时先验证资源。

## 图与定义

每个图持有稳定id，节点持有图内唯一id、kind、parameters，可选runtimeId/enabled/position；边具有稳定id、source/sourcePort/target/targetPort和显式order。数组位置、显示名、坐标不充当身份；重复入口仍执行多次。节点字段契约由`SkillEffectCompiler/SkillCastCompiler`及相应独立编译器维护，网页`model.ts`提供同字段的作者控件。类型化目录分别保存技能、Buff、蓝图、共享轴、弹体和区域，跨图引用使用明确字符串键。

目标、行为输入是数据；cast/execute/next和生命周期输出是执行连接，非法混接拒绝。即时图循环、跨类型依赖递归拒绝；连发、引导、弹体连锁/分裂使用有界领域机制。独立projectile-behavior与根行内策略不能同时拥有同一字段，提取与切换为一次可撤销作者事务。普通传递七模式可以切换，共享定义根类型固定。

默认单位：时间秒、空间米、速度米/秒、角度度、概率0..1、倍率无量纲；界面百分比换算不改变JSON。DTO整数拒绝小数取整、数字字符串、溢出与非有限值，旧内容中的精确整数`.0`身份继续合法；节点参数另按其字段类型校验。所有输入数值必须有限；运行中持续引导使用的无限时长在只读快照中表示null，不将Infinity写入JSON。时间轴、Buff、弹体与角色钟按各自契约推进，网页显示不决定战斗时间。

稳定对外字段的新增默认值、修订哈希与旧载荷按契约评估；内部过时路径直接删除。旧SO/Bake/cache不在正常加载或回退路径。完整DTO到领域映射失败时保留旧会话，不把新参数写入正在运行的旧弹体/轴/Buff。

## 输入复现

输入文件包含formatVersion、stepSeconds=0.02、project、rootSkillId、按atStep排序的commands、endStep和expectedState。输入在同一步按记录顺序执行；配置/随机种子随文件保存，包含完整实体世代，不能只按索引绑定。上限30000步（600秒）/10000输入，超限不导出残缺记录。整数/身份必须一致，浮点末态比较容差0.0001；同Core版本重放不依赖当前磁盘工程，成功后才替换会话。修改运行代码后的跨版本录制结果可能被比较器拒绝，不承诺未来Core版本的结果永远相同。

## 保存、导入与发布

保存允许空工程/断线草稿，要求合法结构与保留引用；预览、配置应用和发布要求完整编译。ETag条件写入和原子替换防止覆盖新磁盘版本，保存仅确认请求时快照。导入原始文本先过C#，再预览替换/合并；合并重分配冲突身份并同步内部引用，可撤销。未知模块/枚举/字段不会静默变成空效果。

源工程宿主默认4192，仅监听127.0.0.1；F6/F7写`Assets/StreamingAssets/Combat`，Unity按F5明确加载。两槽共享工程/依赖版本不一致时保留旧会话，使用同步两槽更新。独立分发包默认保存在包内，可显式指定发布目录；正式内容发布仍写当前内容根的正式project.json。参见SkillLab使用说明、ADR及最终验收记录。
