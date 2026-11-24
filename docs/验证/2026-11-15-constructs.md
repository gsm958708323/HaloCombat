# 2026-11-15 可破坏物、木桶自伤与死亡链爆验收

分支`codex/web-skill-editor-json`，上一重要节点`1e580c7`。协议与取舍见ADR-0018；完整网页编辑器/历史Arena目标仍进行中。

## 内容与边界

只读检查本地Git历史`fae7872`的BarrelComp、BarrelExplosionEffect、SpawnBuffArenaBarrelEffect和TL_3008资产。正式木桶使用SK2008/TL3008，0.1秒生成、0.5秒动作，位于来源前方0.55米，中立阵营、5生命、半径0.25；每5秒自伤1。非同组普通伤害进入护盾/HP前封顶1，同组物体伤害不封顶；死亡半径2.2、拥有者当前攻击×0.15、暴击率.05/倍率1.8，9999伤害引爆同组物体。

新增construct节点、ConstructDefinition/Catalog/Comp、IncomingDamagePolicyComp和construct-chain节点；不恢复旧Barrel专用管理器，也不写按技能分支。生成/死亡/移除端口组合现有范围投递、伤害、Cue、Buff与效果图。构造物保留完整Owner EntityId、来源Skill/CastId、实际生命、自伤年龄和受伤上限。普通移除执行removed而不执行death；卸载静默清理。cleanupWithOwner可显式选择，正式木桶为false，旧Owner回收后不能从复用索引的新Actor取攻击。

每波死亡由World内FIFO队列展开，每物体只处理一次；存活物体每世界上限128，每来源/定义上限可配置；死亡回调波上限256并记录拒绝。与历史递归实现的嵌套事件顺序有明确区别，不声称全部事件顺序相同。范围不要求视线，保留旧桶隔墙范围行为，出生仍执行Core静态障碍净空校验。

网页示例由38增至40，新增木桶连锁和范围引爆。物体参数、端口、运行实例、Owner/组/受伤上限/下次自伤和手动移除均可使用。复制/子图粘贴分配独立物体定义数字身份；发布按所选技能的生命周期图收集效果。正式Arena Fire5/数字5/鼠标第5键读取同JSON技能；木桶使用具名内置barrel视图，爆炸Cue携带实际事件点，不能画在拥有者位置。

## 最终执行证据

| 检查 | 结果 |
| --- | --- |
| Core build/regress | 0错误；ALL S1+S2 REGRESSION PASSED；既有net6.0框架提示保留 |
| CLI同正式文件 | 11技能/10角色蓝图，g1 3伤害事件/22生效伤害/0.59999996位移，JSON RUNTIME PASSED |
| 宿主自检 | 三物体配置、完整连锁/净空/真实伤害与回收通过，既有自检均通过 |
| 前端转换 | 29/29，新增物体复制/粘贴和生命周期连线身份验证 |
| 前端构建 | TypeScript/Vite通过，index-CsylVH6a.js、index-D3x4xkwR.css |
| Unity EditMode | job `53afe03acbac404f8331cad4cd7cef94`，353/353 Passed，0失败/跳过 |
| Unity PlayMode | job `159ed1dd43bf42b882364add26e9310b`，47/47 Passed，0失败/跳过 |
| 实际Arena/场景 | MCP直接启动，Fire5输入生成5HP木桶，x=.55/team0/正确Owner；视图包含DestructibleBody，场景验证通过 |
| 最终Unity Error | 0 |

新增26项EditMode覆盖出生属性/位置/Owner/视图、逻辑周期边界/溢出/Hitstop、自伤死亡一次、伤害上限/护盾/同组绕过/异组受限、三桶链爆、当前Owner攻击、Owner代际失效、可选Owner清理、手动删除/卸载、受阻/数量上限、128桶波次/256回调预算、事件点Cue、坏字段/嵌套重复ID、31位阵营掩码、敌方物体不占刷怪名额及EventBus嵌套分发。PlayMode新增3项，覆盖磁盘发布物体视图/爆炸移除、重载/手动清理与正式Fire5、点Cue和重开释放。

## 网页实操

从浏览器新建40示例临时工程，选择木桶连锁：生成三只5HP物体，运行实例显示Owner=caster、group=barrel、cap=1、nextDecay=4.50秒。实际将生命改为1、自伤周期改为0.2、关闭暴击，再施放：0.28秒三物体仍在，下次自伤0.02；第15固定步/0.30秒第一桶自伤致死，并引爆另两桶，各对target-1造成12伤害，三桶均回收。目标HP964，实际敌人伤害36+物体扣血3，统计总伤害39/6命中。

随后改回5HP/5秒，施放并手动移除一桶；剩余2桶，目标无伤害。真实网页将该稳定木桶技能发布F6、范围引爆发布F7并保存。截图为忽略证据目录中的construct-browser/chain-automatic.png、instances.png、final-instances.png。

验收后按字节恢复原F6/F7，SHA256均匹配备份；恢复原23技能工作工程，浏览器通过“重新载入磁盘”确认23技能与预览就绪，临时1920×1080尺寸已撤销。正式project.json只包含本批实际新增内容，不写回临时HP1/0.2秒设置。

## 跨宿主完整快照

使用实际网页发布的同一F6/F7文件与同一输入：先F6施放、推进30步；再F7引爆、推进1步；最后再推进19步。独立宿主与Unity SkillLab三份完整快照共比较**1786个字段和值，0差异**。

结构、文本、事件顺序和身份严格匹配；数值使用绝对1e-5/相对1e-6容差，避免将两种宿主的浮点输出格式差异当作行为差异。

| 固定步 | 活物体 | 成功施放 | 伤害事件 | 目标HP | 总生效伤害 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 30 | 3 | 1 | 0 | 1000 | 0 |
| 31 | 0 | 2 | 6 | 964 | 51 |
| 50 | 0 | 2 | 6 | 964 | 51 |

51包含三只5HP物体各自实际扣血5，以及三次12点敌人扣血；9999引爆伤害中的过量不计入总生效伤害。事件、顺序、来源、Skill/CastId、实际生命、坐标、自伤年龄、清理和统计均来自真实Core，不靠画面或计时猜测。

## 实际Arena验证

MCP从Arena启动并注入Fire5，5逻辑步后：木桶HP5、x=.55、team0、Owner=Entity(1:1)，对应视图可见。用玩家实际DamageEffect打80点，木桶仅从5变4；再4次同样伤害，死亡事件恰好1次，物体立即失活，Cue4206在x=.55。证据arena-barrel.png、arena-manual.json。重开与失焦、死亡冻结规则沿用当前Arena会话，不能把此检查当作原资源/动画像素复刻。

## 实施中修复

- EventBus原本复用一份可变快照，嵌套发布会丢掉外层剩余监听。改为每次发布独立快照，订阅变化对下一次事件生效；实际木桶死亡与同类型嵌套独立测试验证。
- 目标阵营掩码暴露NodeParameters.Integer先转float的精度缺口；现在用double校验并精确转换到int，team30和int.MaxValue均实际验证。
- 旧SO迁移fixture只描述原四Cue与默认敌对范围；保留原Cue严格值比较，新增Cue独立测试，零mask规范化为旧隐式敌对，不因追加内容破坏原签名。
- 当前浮点HP在1e9附近不能表示7.5/30点差值，拥有者攻击测试改为1000HP独立靶；正式训练桩1e9保持旧配置，不能拿它证明微小扣血精度。

## 后续边界

ENG-003关联N02/N03：原爆炸TargetHitstop3与EvHurt级别反馈尚未映入当前正式内容，不能用全局Hitstop冒充目标独立顿帧；原Cue资源/动画仍待迁入。历史13技能剩余内容、AoE移动/弹体交互、角色随机成长、ControlTags/动作速度和完整表现继续推进。完整运行包/内容包导出、作者节点定位/输入复现及视频最终视觉交互仍未完成。

本机原始证据在Git忽略目录Tools/SkillLab/.local/verification：unity-edit-constructs.json、unity-play-constructs.json、construct-build.log、construct-regress.log、construct-host.log、construct-cli.log、construct-authoring.log、construct-web-build.log、construct-browser各发布文件/host与unity快照/comparison.json/截图。提交仅保留源码、JSON、测试、ADR和本验收记录。
