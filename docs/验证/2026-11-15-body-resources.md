# 原实体资源与 SkillLab 发布修复验收

日期：2026-11-15；分支：`codex/web-skill-editor-json`；上一节点：`f2b4649`。历史依据为`fae7872`的BuffArenaViews、Cues和角色锚点；决策见ADR-0028。

## 资源链路

恢复16个根Prefab及其依赖，共69项资源/对应meta，约17.4MB，无缺失GUID。11个body目录覆盖木桶、普通/敌方/回旋/传送/炸弹弹体、吸收球/延迟炸弹/燃烧/黑洞/护盾。12种Cue共用9份粒子Prefab。17份材质保留贴图和GUID，转换到URP Lit/Unlit/Particles Unlit。

body与actor目录有独立类型：角色要求Animator/合法状态，body要求Renderer且无角色Animator、玩法脚本或物理组件。Projectile/AoE/Construct按view键发布，真实逻辑Y、朝向和区域半径比例驱动显示，旋转/粒子按ActorTime推进。SkillLab从同一发布会话读取目录，Unity资源缺失时拒绝候选世界，已有会话保留。

角色Body/Head/Muzzle从旧组件提取为JSON路径与局部偏移，无旧脚本回退。Cue区分来源/受击者和指定点，单次/循环可挂锚点；循环按完整EntityId/CastId释放。恢复原时间轴枪口、回旋头顶爱心和Buff循环，射击/确认轴的Cue顺序据旧Payload核对。

历史Git树没有wav/ogg/mp3或AudioClip文件，只有MuzzleFlash等音效键。当前保留明确音频缺口，不生成替代音频冒充原资源。

## 用户报告的发布问题

第一处实际问题是修订校验。用户发布包中随机目标坐标为2.9133646，Unity Mono转float再序列化为2.91336465，旧实现因此错误报告“Published content has changed”。现在直接校验文件内project JSON载荷，独立执行原DTO与语义校验；原用户发布包89a3ffe3已实际读入，修改载荷依然拒绝。

第二处是有效的共享冲突保护：F7技能依赖basic-shot，单独把F6普通射击改为新参数时，F7仍持有旧basic-shot。现在网页单槽发布后显示明确原因并提示“同步两槽”；同步保留各槽原技能根，按当前工程重建共同依赖，写入前整体编译校验。空槽组/其他工程拒绝，不静默覆盖为同一个技能。

SkillLab新增各槽已加载技能/修订、磁盘待载入提示和Reload按钮。发布写入磁盘后，F5/Reload才替换会话；保持执行中的旧实例不可变。

## 验证结果

| 检查 | 结果 |
| --- | --- |
| 相关Unity MCP EditMode | 86项完成、无失败；job `5ec3609af7ec4d819d81b33b27bd311e` |
| 相关Unity MCP PlayMode | 11项完成、无失败；job `6cf06da8156948b5b6c007e3ab19d822` |
| 前端作者检查 | 50/50 |
| 前端/宿主构建、宿主自检、Core regress | 通过 |
| 物理源码行数 / 源码与YAML差异检查 | 均不超过500 / 通过；源ASCII FBX保留原导出空白 |

EditMode包括资源引用/类型/旋转/锚点边界、不可变映射、受击者来源、原Cue/旋转、载入前验证失败保留会话、浮点发布载荷与篡改、原发布包读取。PlayMode覆盖真实磁盘两槽原Prefab、9→新速度重载、待载入与版本、坏资源保留、URP材质/原回旋/枪口锚点及已有炸弹/翻滚/共享弹体清理。首次粒子初始化发现设置种子前未停止播放，已调整为先StopAndClear再设置；相关11项重跑通过。示例弹跳无资源键时仍使用原逻辑示意视图，测试不把它误当原模型。

## 网页实际发布到 SkillLab

使用用户当前工程，不另造接口成功回执：网页将basic-shot弹速设9，点击“同步两槽”，SkillLab读取F6 basic-shot /527c462d与F7原技能2dac5590-9c19-45f4-bcd6-df615c30413a，实际弹速9。

随后网页改18，仅发布F6：网页显示共享冲突；SkillLab检测Pending=true，重载失败并保持527c462d和已有世界。点击“同步两槽”后，SkillLab重载成功，F6版本b2114614、Pending=false，实际弹速18。HUD截图显示各槽技能和修订，当前逻辑15步/一枚弹体。

原资源另在Arena实际显示FemaleGunner、Boomerang及头顶爱心，枪口Cue与URP材质由真实场景测试验证。证据位于Git忽略目录`Tools/SkillLab/.local/verification/body-resources/`，包含恢复清单、网页发布文件、9/冲突/18读取结果、测试/日志和网页/Unity截图。

验收前备份了用户本轮最新工程和槽，最终按字节恢复；不使用较早节点备份覆盖用户后来发布的槽。正式资源配置未写入网页试验参数，Unity回到SkillLab编辑状态。

## 剩余范围

原音频文件缺失；双相位Motor/精确时序、完整内容包导出、作者节点定位、输入复现和视频最终视觉与流程对照继续，不将资源链路接通等同整体完成。
