# JSON 角色与 Animator 验收

日期 2026-11-15，分支 `codex/web-skill-editor-json`，前一节点 `e85bb0d`。本批按 ADR-0022 接入原人物资源和动画状态；整体网页编辑器复刻继续进行。

## 实现范围

- `runtime.views` 目录、大小写敏感语义映射、Actor View 引用、缩放和局部朝向，编译为独立只读定义。
- 显式 Timeline 的可选 animation 键，导演和 PoseSample 输出动作相位、完整 CastId、ActorTime、移动意图与移动限制。
- Unity 候选会话启动前检查 Resources、单 Animator、有效 Avatar、真实状态和单 Clip。资源只显示，不包含旧 MonoBehaviour、碰撞体或 Rigidbody。失败保留旧局。
- 单一相位采样：Timeline 驱动动作，ActorTime 驱动循环/受击，wall-time 驱动死亡；独立顿帧、二倍动作、同状态重复施放、方向翻滚、打断和重开释放均覆盖。root motion、AnimationEvent、旧 Controller 自动过渡不参与玩法。
- 共享材质的闪白改用 MaterialPropertyBlock，不克隆、修改或销毁原材质。原程序化模型继续用于木桩、物体和未迁入资源的角色。
- 网页增加角色视图选择、Prefab 键、缩放、朝向、状态路径和时间轴动画编辑；合并工程重映射目录 ID 与 Actor 引用；发布闭包保留目录，F6/F7 和正式内容校验 View 版本一致性。

## 原资源依据

从本地 Git `fae7872` 的 BuffArenaViews GUID 定位 FemaleGunner / MaleGunner，两者实际使用 CharacterAnimator。依赖闭包恢复 52 项资源及其 meta（25 份动画 FBX、Controller、人物/枪械 Mesh、Prefab、5材质及纹理），保留原 GUID。两个人物迁入 `Assets/Resources/Combat/Actors`；旧 BuffArenaViewAnchor 及嵌套 Prefab addedComponent 引用彻底删除，不恢复旧表现管理器或 SO。

五材质转为项目实际 URP Lit，保留原反照率、金属度和遮蔽贴图。原缺失 emission 纹理不在历史提交中，清除无效引用并保持发光关闭；该部分全套原表现仍归 N08。原角色服装/发色和枪械已在实际相机画面验证。

## 最终检查

| 检查 | 结果 |
| --- | --- |
| Unity MCP EditMode | **441/441**；job `74adf056570143f584da265979787e6d` |
| Unity MCP PlayMode | **62/62**；job `e4d83a7a73fc4029b37bef759d386c12` |
| 前端作者检查 | **31/31** |
| 前端生产构建 | 通过 |
| C# 宿主 self-test | 通过，含正式 Arena 120步确定性 / 150伤害3命中3施放 |
| Core regress | ALL S1+S2 REGRESSION PASSED |
| 正式 JSON CLI | 12技能、10蓝图；原 G1 22伤害/3命中/0.6米 |
| Unity Error | 最终0 |
| 源码行数 / diff | Combat源码、测试、网页TS/TSX均≤500行；源码/配置/文档diff-check通过 |

新增17项 EditMode 覆盖独立只读绑定、大小写往返、资源路径、缺失/重名/未知引用、身体/动作/方向语义、非法缩放、发布闭包和版本冲突保留旧会话。新增6项 PlayMode 使用真实原资源，验证骨骼、材质、状态/相位、重复施放、方向、受击/死亡、独立停止、动作倍率、失败重载与重开视图数量。旧 Arena 场景测试由程序化子物体路径断言改为真实 Animator/SkinnedMeshRenderer 断言。

原生 Unity YAML/meta 保留其序列化空字段后的空格，不对历史导入设置做无关格式重写；源码/配置/文档单独检查空白错误。

验收修复了 JSON 默认大小写命名对内容令牌的误改，以及 Animator.speed=0 时状态 length 为 Infinity 的实际差异；当前使用局部字典策略和源 Clip 时长。MaterialPropertyBlock 仅在 Bind 主线程创建。最终审阅补齐 Arena 暂停信号，死亡动画按实际 wall time 推进，暂停或同帧重复采样不会错误消耗动画时间。

## 浏览器到 Unity 的真实发布

在原网页打开正式内容，将 G1 动画语义 Fire 改为 RapidFire，玩家模型倍率1→1.1，保存并真实发布正式 JSON 与 F6。Unity MCP 从这份磁盘文件加载到独立测试会话，F6施放后推进10步：

| 投影 | Unity 实测 |
| --- | --- |
| State | RapidFire |
| Clip | ShootAutoshot_HG01 |
| CastId | 1 |
| 相位 | 0.363636345（0.2 / 0.55） |
| Prefab 实际缩放 | 0.55 / 0.55 / 0.55（原0.5×1.1） |
| 逻辑/视图位置 | x0.36 / y0 / z0 |
| 男角色 | 原 MaleGunner、Stand、IdleBattle01_HG01 |

另通过 MCP 直接采样原 RunFWD：ActorTime 0→0.2，Phase 0→0.299999982；重复渲染和顿帧保持相位。PlayMode 正式二倍速度技能5步相位1/3，独立停止3步不变，再5步相位2/3；重开销毁旧视图，只留一个新玩家且状态 Stand。

证据在 Git 忽略目录 `Tools/SkillLab/.local/verification/actor-animation/`：`web-formal.json`、`web-f6.json`、`web-actor-bindings.png`、`unity-original-actors.png`、`unity-rapid-fire.png` 以及独立运行文件。未保留测试修改到正式文件。

## 恢复与边界

原23技能工作工程、F6/F7及正式内容都按备份字节恢复。正式内容恢复后保留本批新增两份原角色目录、11个动画语义绑定，玩家倍率回到1，G1动作回到Fire。浏览器已重新加载原23技能工程；测试资源与截图移入忽略证据目录，Unity 退出 PlayMode。

N03的四类Clip、离散Payload、ControlTags、动作速度和动画状态链路已验收。N02其余旧十三技能的数值/连段、N08弹体/AoE/Cue/音频等剩余原资源、完整内容包/运行包、作者定位/输入复现和视频最后视觉交互对照仍未完成。
