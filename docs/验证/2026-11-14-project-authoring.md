# 2026-11-14 工程编辑与引用管理验收

分支 `codex/web-skill-editor-json`，阶段提交基线 `be3ec60`。本批按 ADR-0014 实施；本记录不代表正式 Arena SO 删除迁移、全部节点或参考视觉已经完成。

## 执行结果

| 检查 | 当前证据 |
| --- | --- |
| 前端纯工程操作 | `npm run test:authoring`，20/20通过；包含改名冲突、禁用/隐藏引用、复制新身份、删除替换、子图粘贴、顺序、目标端口重连、合并闭包、120次位置更新合成一次撤销、保存竞争及失败 |
| 前端构建 | TypeScript/Vite通过；最终 `index-B2o5p2mX.js`、`index-EjkFXpdO.css` |
| 宿主构建与自检 | 0警告/错误；原战斗自检及新增空草稿、缺版本/旧版本拒绝、两个并发写入只一个成功、坏结构保留文件全部通过 |
| HTTP实际失败路径 | 缺If-Match返回428、旧ETag返回409、坏结构返回400；三个请求后磁盘内容与请求前逐字符一致 |
| Core回归 | `dotnet build Combat.csproj`通过，既有net6.0 EOL提示；`regress`输出 `ALL S1+S2 REGRESSION PASSED` |
| Unity编译/控制台 | Coplay Unity MCP刷新及Domain Reload后ready；本批最终Error 0 |
| EditMode最终全量 | job `eb1c6fd2b38343b290588aa79e03f4dc`，261/261 Passed、0失败/跳过，包含12项新的工程结构/模板/发布检查 |
| PlayMode最终全量 | job `5caae6f0d52448f9a227903fc7ebf473`，32/32 Passed、0失败/跳过 |
| 实际网页发布包跨端 | 网页F6/F7原文件，宿主HTTP与Unity真实SkillLab PlayMode，各30/120步，4份完整快照、2847字段/数值一致，数字容差0.00001 |

## 实际浏览器操作

建立独立“工程编辑验收”工程，完整保留原本工作文件和F6/F7备份。实际完成：

- 新建空工程、保存、刷新，恢复0技能空工程，发布/施放禁用且可以继续新建。
- 从灼烧模板建立技能和共享Buff；改Buff ID为`verified-burn`，节点引用同步、runtimeId不变；对话框实际显示F6包含原内容。
- 复制Buff、删除原Buff并将所有引用替换为副本，JSON中的定义和引用同时改变；一次撤销恢复原Buff、引用和图选择，再重做恢复删除。
- 技能改ID、复制技能及独立节点/边身份、改名称、删除、撤销。图内Ctrl A多选后复制粘贴，已有目标/施放根跳过，仅粘贴两效果节点及内部连线；Delete一次移除子图，撤销一次恢复全部，继续撤销恢复粘贴前。
- 用端口实际拖动新增投射物wall→Buff连线，连线总数3→4，输出连接数0→1；撤销回3。连线面板删除hit边，断线草稿实际保存成功；一次撤销恢复完整图及三条边。
- 实际拖动目标卡片从(0,50)到约(26.1574,-31.5495)，一次撤销完全恢复(0,50)，一次重做恢复最终位置。没有将拖动中间帧占满历史。
- 大小写不敏感的ID搜索、分类与数量；技能图和Buff图独立视口，切换/保存/刷新恢复。另覆盖同工程/同图ID覆盖导入，准确恢复`translate(82px,104px) scale(.62)`。
- 从召唤模板补入攻击技能和蓝图，将`echo-gunner`改为`verified-gunner`，生成节点同步；将攻击技能`basic-shot`改为`verified-basic`，蓝图`skillId`同步。工程完整C#编译通过。
- 保存、实际下载JSON、通过文件选择器回读同文件并合并：4→8技能、文本和runtimeId均8个唯一值，导入Buff/蓝图不绑定旧同名定义，无缺失引用；一次撤销回4技能。
- 新建工程选择恢复全部示例，实际显示34技能；一次撤销回原23技能。随后保存恢复原工作工程。
- 1440×900检查桌面布局，测试完成恢复浏览器原尺寸；最终页面JavaScript错误日志为空。完整参考视觉仍是独立后续验收，不以此检查声明像素对齐。

引用弹窗截图：`Tools/SkillLab/.local/verification/authoring-reference-impact.png`，实际显示一个蓝图入引用和F6/F7两个发布包；最终桌面截图`authoring-final-desktop.png`。

## 同文件宿主 / Unity结果

网页F6为改名后的`verified-basic`；F7为“蓝图身份验证”，包含`verified-gunner`蓝图和对`verified-basic`的引用。两槽来自同一工程，分别在独立重载会话施放。场景/种子/输入/固定步长相同，双方使用发布文件里的真实闭包。

| 槽/步数 | 两端实际结果 |
| --- | --- |
| F6 / 30和120 | 80伤害、1命中、1投射物；SkillId为`verified-basic` |
| F7 / 30 | 80伤害、1命中、2投射物，真实分身使用改名蓝图/攻击技能 |
| F7 / 120 | 464伤害、5命中、6投射物，完整事件/实例/Owner/蓝图/技能/位置与宿主一致 |

Unity通过MCP打开实际`Assets/Scenes/HaloCombat/SkillLab.unity`、进入PlayMode，用SkillLabRunner读取原始发布路径并推进完整逻辑步，没有替换为测试内写死定义。比较全部字段和数组，不仅比较伤害数字。结束退出PlayMode、恢复Arena场景，不保存运行状态。

## 验收中发现并修正

首轮EditMode257/260通过。两个错误路径使用了文本ID而原契约要求JSON数组位置；已保留原断言并修正路径。另禁用召唤引用被新的草稿引用校验当作运行必需项，导致闭包裁剪蓝图后发布包无法重读；现在草稿检查保留的全部引用，运行编译只解释生效字段，禁用分支不会引入虚假依赖。最终全量通过。

第一次浏览器验证发现受控React Flow节点未保留测量尺寸，使首次适应和视口恢复不可靠；测量只放网页视图状态，未写入领域模型。模板接口复用发布出口导致作者位置被清零；拆分模板闭包选择与运行位置规范化，新增回归确认模板保留原布局且发布不修改源对象。视口恢复另纳入工程ID和已保存视口变化，覆盖同ID工程替换。目标数据边重连检查排除其旧边，避免把自己当成重复占用。

## 文件和工作状态

原始23技能、Buff、蓝图和预览配置已经恢复，技能/Buff/预览数据与备份一致，仅增加编辑器视口元数据；原F6/F7逐字节恢复，与阶段提交无diff。独立验收工程和新发布包保存在`.local/verification`，不覆盖预置34模板。

该目录另有`web-authoring-checks.log`、`web-authoring-build.log`、`host-authoring-build.log`、`host-authoring-checks.log`、`unity-edit-authoring.json`、`unity-play-authoring.json`、`browser-authoring-project.json`、`authoring-published-f6/f7.json`、`authoring-host/unity-{f6/f7}-{30/120}.json`及`compare-authoring.ps1/.mjs/.log`。证据目录Git忽略，本记录保留job及可复核的结果。

剩余范围：剩余产品行为与正式运行模型（换弹、木桶、原Timeline/Combo/角色资源配置）、正式Arena/CLI JSON迁移和SO删除、事件反向定位、完整输入复现与最终视觉对照。工程编辑完成不替代这些交付。
