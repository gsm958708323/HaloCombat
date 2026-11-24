# 作者节点与实体反向定位验收

日期：2026-11-15。对应ADR-0031、K05，完整编辑器目标继续进行。

## 来源链

编译结果按效果实例建立GraphKind/GraphId/NodeId来源，不包装或重排原效果。Core只有可选IEffectObserver；每个世界独立维护诊断栈与执行序号，嵌套/异常恢复父来源。运行事件快照增加调度步、节点、执行/父执行序号和来源/目标/实体完整身份；伤害仍来自EvDamage，节点执行不能冒充伤害成功。日志沿用500条容量与实际执行顺序。

共享Timeline/Projectile/Area、延迟Buff、重复效果位置和出生/终结/回收均按真实效果或定义定位，未知来源保持空值。生成动作的Melee/Dash使用不透明的编译诊断site，Core不读取作者文档。来源表不进入JSON、不改变发布修订；重排布局不改变节点身份。

## 网页实际验证

在临时页操作，原用户编辑页保留。用户工程的灼烧验证施放后，事件显示burn/period-damage，点击打开Buff图、选中该节点；点击目标圈出完整身份8589934593。预览仍是原灼烧技能，运行时间和伤害状态保留。已回收弹体21474836481仅显示已回收，未选中新世代实体；点击其节点仍能定位原projectile。

打开正式内容并执行双发射击，实际22.5伤害/2命中/2弹体。伤害事件指向projectile/arena-normal-projectile/damage，保留调用技能arena-double-shot和Cast1；点击后选中共享图damage，预览时间保持150.44s，预览根仍为双发射击。重置清空实体选择，新动作实例可定位定义。

实际下载单技能包修订a9f1b291c35d，包含1技能/1Buff/1共享弹体；确认文本不包含诊断字段。MCP在SkillLab实际加载该文件，与宿主使用同一根和输入，20/40/80步完整快照2614个值对照，最大数值差3e-8，按1e-5容差通过；最终22.5伤害/2命中。两次伤害均定位共享projectile/arena-normal-projectile/damage，保留arena-double-shot/Cast1和相同来源/目标身份，执行序号分别5和9。

## 检查

前端构建与作者检查54/54通过；Core regress与宿主自检通过，后者覆盖延迟Buff伤害来源/完整身份和弹体回收。全量EditMode初检613项，612通过；新三连发测试只统计damage漏掉critical，修正统计后相关9项通过（job f84603c0cb8945f0a23506812364c2eb）。补充嵌套技能事件、异常恢复和结构化来源键后，最终相关29项完成、无失败（job5c114877613847ef88edcb40b3b6e94f）。

相关PlayMode26项全部通过（job8eac017970e1455e8c33cfa18ad30349），含同发布文件Buff/回收来源、重置清理和既有SkillLab/共享Timeline/共享Projectile/Buff/Motor场景。独立世界来源栈、重复执行序号、共享图与调用技能分别验证。

证据位于Tools/SkillLab/.local/verification/author-trace，截图、实际下载文件、日志和对照快照不进入提交。用户工程和F6/F7按本批新鲜备份逐字节核对未改动，临时浏览页关闭，原页保留；Unity恢复SkillLab编辑状态、Error0。输入复现、完整历史时序与最终视频流程仍继续。
