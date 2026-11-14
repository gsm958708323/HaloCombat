# 2026-11-15 当前运行场景JSON迁移与SO删除验收

分支`codex/web-skill-editor-json`，上一重要节点提交`04365d5`（工程编辑）。本批从2026-11-14实施，2026-11-15收尾验收，协议决定见ADR-0015。当前目标仍未全部完成。

## 本批完成范围

正式运行内容为`Assets/StreamingAssets/Combat/project.json`，沿用SkillProjectDocument、图节点和SkillEffectCompiler，没有人工维护第二份业务定义。6技能：G1、G2、闪避、追踪、光环、召唤；2共享Buff，6普通角色加1个从共享召唤蓝图转换的角色；Motor和4个Cue。

- 显式Timeline保存独立数字身份、稳定Clip/Payload身份、开始/结束/位移/Steer/Hitbox偏移、有序可重复的效果入口；四类原Clip仍由原TimelinePlayer/handlers推进。参数化技能原路径保留，显式Timeline不再套一层自动延迟/连发/引导时序。
- Combo保持边表、输入、多个前驱、RequiredTags、优先级和目标技能；正式RuntimePlayerDriver经SkillCastComp校验状态、冷却、资源和Cancel窗口，成功才消费输入，失败保留输入。AI的ActPlaySkill在有SkillCastComp时也经同一守卫，没有独立技能状态机。
- RuntimeActorFactory只消费Core领域定义，按玩家/目标/AI/召唤组合组件；保留实际属性、感知参数、BT配方和技能槽。未知蓝图失败。召唤属性及技能从共享蓝图编译，不与runtime段重复保存。
- V1–V4/TrainingCamp使用UnityRuntimeContent/RuntimeProjectLoader严格读文件；完整验证后替换世界，旧世界卸载组件和队列。缺文件或坏引用保留已有会话，首次加载失败直接报错，删除SO/代码回退。
- Cue具有明确cube/prefab类型、寿命、资源/音效键；Unity加载时验证资源，播放时消费绑定，缺资源不能退回方块。迁移前4个Cue的Prefab字段均为空，因此JSON明确配置cube。模型用具名view键与Unity原表现实现绑定，JSON不保存Unity对象。
- 网页可打开正式文件、直接编辑显式时间轴、在runtime面板校验角色/Combo/Motor/Cue并发布正式内容；复制/删除节点会更新Timeline效果入口，复制/合并/改名同步角色和Combo引用。运行包仍从同一语义源产生；F6/F7携带一致Motor/Cue，冲突拒绝。
- CLI `dotnet run --project Combat.csproj -- json Assets/StreamingAssets/Combat/project.json [skill-id]`实际加载同文件、创建角色/组件并运行；CodeCombatContent只留在独立教学Demo中。

## 删除与原值对照

迁移前用Unity MCP从实际CombatDatabase.asset执行BakeAll，反射提取已注册定义，保存完整Timeline、Clip/Payload、效果顺序/字段、弹体、区域及Buff修正/回调、召唤参数、Combo、Motor和Cue的签名。558个结构/值比较位置与迁移后的签名对齐；fixture为`Assets/Tests/Fixtures/legacy-combat-content.json`，仅供测试，不参与运行加载。

签名明确排除作者Name/Key、OriginContentKey、BT运行原型以及新增PresentationKind/ResourceKey；TargetEffects的当前目标包装在比较时展开。AI/共享原型独立性、来源清理和真实行为另由EditMode/PlayMode覆盖，不能将静态值比较声称为全部玩法或像素复刻。

已删除Config根目录29个SO作者文件及其meta、GenerateDefaultDatabase生成器及meta、Generated下31个SO资产及meta；清除五个场景的数据库/开关字段，使用`_contentFile: Combat/project.json`。Config asmdef设`noEngineReferences:true`。TimelineSO与ComboTableSO两个纯C#类更名为TimelineDefinition/ComboTableDefinition，没有旧名兼容层。

扫描Assets场景/Prefab/asset对31个已删除资产GUID引用为0；源码内ScriptableObject、CombatDatabaseAsset、SoCombatContent、_useSoDatabase、GenerateDefaultDatabase及两个旧类型名均0命中。

ENG-001的602行DefinitionAssets.cs已删除。ENG-002的Demo聚合按Tag/Attribute/Buff、Movement/Timeline、CombatEffects、SeasonOne四组拆分，原类名、入口和断言保留；全仓Combat脚本/Editor/测试无超过500行的C#文件。

## 最终执行证据

| 检查 | 结果 |
| --- | --- |
| Core build/regress | 构建通过；既有net6.0 EOL提示3条、0错误。regress为ALL S1+S2 REGRESSION PASSED |
| CLI真实文件 | 6技能/7角色；g1实际3次伤害、22生效伤害、原位移0.59999996，JSON RUNTIME PASSED |
| 宿主build/self-test | 0警告/错误；前批全自检及同正式文件Actor/Combo/Timeline/真实伤害22检查通过 |
| 网页纯转换 | 24/24，新增runtime改名全引用、显式轴复制入口、新身份、删节点入口/撤销和合并闭包 |
| 网页build | TypeScript/Vite通过，最终index-o1dr2CSB.js、index-D3x4xkwR.css |
| Unity EditMode | 最终job a46b37dab6f04701be9e3ed9a866590f，277/277 Passed，0失败/跳过；新增16项参数化runtime检查 |
| Unity PlayMode | 最终job 40273877370e43f8aed71ca37b316940，35/35 Passed，0失败/跳过；包含原32项与新增正式输入/坏文件保留/训练场重置 |
| Unity项目验证入口 | MCP调用FinalVerification.Run成功；验证原教学场景与V1–V4 JSON绑定，无旧数据库依赖 |
| Unity最终Error | 0 |

## 真实网页修改→磁盘→正式Unity

浏览器点击“打开正式内容”，显示6技能、G1的3区间/2触发。直接将移动区间MoveX由0.6改为0.9、保存，再点“发布正式内容”，实际StreamingAssets文件字段为0.9。不是临时测试内构造配置。完整画面见`.local/verification/runtime-timeline-published.png`。

共享网页C#预览用该文件；正式Unity从V2Melee场景启动，CombatRunner.BuildWorld读取同文件，通过玩家InputBuffer注入Attack。两端caster设(0,0)、目标(2,0)/HP1000，使用默认一致种子/固定0.02步。正式世界经原真实输入驱动，网页经SkillCastComp入口，比较相同配置在不同入口的结果。

| 固定步 | 两端X | 目标HP | 生效伤害/次数 | 活跃Actor数 |
| --- | --- | --- | --- | --- |
| 25 | 0.8999998 | 990 | 10/1 | 2 |
| 50 | 0.8999998 | 990 | 10/1 | 2 |
| 100 | 0.8999998 | 988 | 12/2 | 2 |

合计18个投影数值一致，容差0.00001；本批正式入口与SkillSimulation角色模型不同，因此不声称完整快照字段逐一相同。前批SkillLab同模型完整对照仍有独立记录。

验收后恢复正式JSON原位移0.6，保留单独runtime-browser-published.json作为0.9版本证据；恢复用户23技能工作工程及原F6/F7，Unity退出PlayMode并恢复Arena编辑场景，没有保存测试运行状态。

## 实施中修复

- 原值比较发现单发spread、Auto中心、非Tag驱散筛选默认Tag和空音效键等差异，均按实际旧值修正；非Hitbox的空效果数组保持原[]。原签名浮点由JSON解析统一表示后严格DeepEquals。
- 首轮PlayMode33/35通过；新测试错误地假定教学玩家起点为0，而CombatLesson会在创建后布置场景，改为验证0.6位移增量；保留真实伤害断言并声明两条正常DMG日志，未吞掉异常日志。最终35通过。
- 配置迁移后验证入口缺少旧教学Builder的namespace引用，编译检查发现后修正。
- F6/F7组合原先丢弃runtime元数据，现保留Motor/Cue并验证两个槽相同版本；回归覆盖冲突保留原会话。

## 仍未完成的原始范围

当前未跟踪Arena.unity引用的历史脚本、数据库和View GUID在当前分支源码/资产不存在，没有可运行的ArenaBootstrap/Session。此次迁移覆盖当前真实可运行的V1–V4和训练场，不声称恢复Arena、原缺弹换弹、木桶链爆、旧动画资源或历史分支全部玩法。

完整目标仍需恢复/实现正式Arena入口及剩余产品能力、相机/动画和资源规则、完整内容包导出、事件定位/输入复现及视频界面逐项对照。SO已删除不等于整个1:1编辑器已经交付。

日志与JSON证据在Git忽略目录`Tools/SkillLab/.local/verification`：migration-core-build/regress、migration-cli-json、migration-host-build/checks、migration-authoring-tests、migration-web-build，unity-edit-runtime-migration.json、unity-play-runtime-migration.json、runtime-browser-published.json、runtime-preview/unity-{25/50/100}.json，旧资产备份so-migration-baseline。最终提交保留本验收文档、fixture和测试，不提交本机产物。
