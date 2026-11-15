# JSON 文件导出与回读验收

日期：2026-11-15。对应 ADR-0029，范围为 L04/L12，完整编辑器目标继续进行。

## 文件契约

完整工程保留编辑状态与布局，允许空工程和断线草稿。当前技能包沿用现有发布协议，包含根与真实依赖闭包；直接作为 SkillLab 槽文件。完整运行内容沿用 SkillProjectDocument，要求完整编译和正式角色定义，保留全部运行数据并删除 editor/position。没有新增第三套人工配置或兼容路径。

导入接收原始文本，C# 先拒绝重复字段、未知协议/属性和坏发布修订，再返回作者文档及独立的布局缺失标记。零坐标的工程布局仍原样保留；运行包派生布局只改变视图，不改身份、参数和执行顺序。文件名净化独立于内容身份。

## 实际浏览器闭环

在独立临时页操作，保留用户原编辑页。实际下载并通过选择文件回读：

| 文件 | 实际结果 |
| --- | --- |
| halo-skill-lab.project.json | 23 技能/1 Buff/1 蓝图，完整布局/选择/预览回读；内容与开始时用户工程相同 |
| halo-skill-lab.basic-shot.skill.json | 根 basic-shot，1 技能闭包，修订 9a2df1db1077；合并增至24技能、撤销回23；替换为1技能、自动布局、撤销恢复 |
| halo-combat-runtime.runtime.json | 21 技能/5 Buff/1共享轴/6弹体/3区域/9角色，完整修订4e14a75ae324；回读恢复全部定义与正式配置，生成可读节点布局 |

示例作者工程无正式角色定义时，完整运行导出明确提示从正式内容开始，或使用单技能包验证。空文本、null、数组与原始重复属性经实际HTTP请求均400，未写磁盘。

## Unity 与宿主

把实际下载的单技能文件按字节复制到独立验证目录的 f6.json。MCP 在真实 SkillLab Play Mode 中配置该目录并 ReloadPublished，显示根 basic-shot 与相同修订。15/60/120 步完整快照与同文件的宿主比较1285个值，最大数值差5e-8，按1e-5浮点容差无差异；最终1施法/1弹体/1命中/120伤害。

完整运行文件临时放入 StreamingAssets 的独立验证目录，通过现有 ArenaBootstrap.Configure/Reload(false) → UnityRuntimeContent → RuntimeProjectLoader 加载。训练模式实际固定输入90步保持、30步释放；共120步，3施法/3命中/189伤害，2角色，与原正式JSON在同一Unity进程的状态、资源、动作时间与事件对照一致。验收脚本首次误用了不存在的场景目录，已更正为 Assets/Scenes/Arena.unity 后完成上述实际验证。

Unity资源仍通过键引用；导出JSON不会嵌入Prefab、材质或音频。原音频资源缺口仍归N08。

## 检查与恢复

前端作者检查52/52，前端与宿主构建、宿主自检通过。相关PlayMode11/11，job `14b4818d598d4885915279958e5de0bb`。受影响EditMode最终覆盖26项：job `408c051ab19b4899b8ef2c8967f29c40`完成26项，其中25通过，文件名测试使用了错误的示例技能ID；改为正式工程根并增加Windows保留名称覆盖后，只重跑该受影响项，job `c343ec9a7b0149e999f628e8639aa04b`完成1项、无失败。此前25项基础检查另已通过（job `af50bad9d58641ca9b82ab0735b84158`）。

Core regress通过。最终Unity Error日志0，SkillLab编辑状态。源码文件不超过500行，暂存差异检查通过。证据在Git忽略目录 Tools/SkillLab/.local/verification/json-exports，包含三种实际下载文件、完整快照比较、Arena对照、构建/自检日志与网页/Unity截图。

用户工程与两槽未被验证覆盖，结束时与新鲜备份字节一致。Unity恢复SkillLab编辑状态；临时StreamingAssets目录移入本地证据归档，未提交验证文件。
