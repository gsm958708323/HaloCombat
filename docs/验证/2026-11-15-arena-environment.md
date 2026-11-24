# 2026-11-15 历史环境与相机验收

分支`codex/web-skill-editor-json`，上一节点`ee9cf30`。对应ADR-0040。原技能794值基线以外，新增三角色/规则/Motor/相机84值来源夹具；来源固定在原历史提交，Prefab身份比较实际AssetDatabase GUID。

## 发现与修复

原训练靶绑定buff_enemy_view，当前仍为教学stake，现恢复原MaleGunner骨骼模型；保持1e9HP、静止、不还手。原敌人AcquireRadius20恢复。原相机站立视点1.6、倒地/死亡0.55、Hz8平滑、分级冲量及FOV/抖动恢复；角色和规则本身不依赖相机。现有公开JSON默认值不暗改，新增可选字段省略默认值，两槽旧发布内容仍能读取。

## 自动与Unity实际检查

- 前端作者检查100/100通过；本批网页只扩展可选类型，未改变UI逻辑，沿用上一批构建。
- 宿主以独立输出目录编译，0警告/0错误，未中断4192用户试玩服务；自检通过。最后索敌半径修正后按同一宿主自检再次通过。
- Core全部S1/S2、clock/spawn回归通过。
- Unity Coplay MCP编译：发现新相机回调误读已迁入DamageResult的Feedback、以及旧测试Sample调用缺渲染参数；修正源码和实际调用后编译Error0，不加旧接口shim。
- 相关EditMode112项完成、status=succeeded、无失败，job`85c319678a4d406883f47ad038c78e3e`；返回result=null，依据明确终态/完成数/失败列表记录，不虚构summary字段。
- 环境夹具追加完整默认属性/索敌半径后最终相关20/20，job`bfeb3977ff8a49f58e1d2bdc9de277c5`。涵盖JSON默认/覆盖/范围、角色GUID/成长、规则/Motor/相机与真实AI成长。
- PlayMode26/26，job`05b04801f0d146b8b35261e6e48b3150`。包含相机首次定位/独立平滑/倒地高度/本地伤害分级/暂停衰减/关闭shake/重开、原训练模型真实Animator和SkinnedMeshRenderer、原Arena/输入/Buff/翻滚/炸弹、双相位Motor与资源动画回归。未再次机械全量重复。
- 相机数学测试独立按原指数式、分段时间不变性、负delta、各命中等级、归零、实例隔离验证；JSON非法配置精确定位字段。

## 现场观察

Unity真实加载`Assets/Scenes/Arena.unity`，进入PlayMode，切训练并推进1步；现场两角色均有真实动画，位置为玩家(0,0,0)、训练目标(4,0,0)，相机(0,4.1,-2.5)、FOV60，无加载错误。截图`Tools/SkillLab/.local/verification/arena-final-audit/arena-restored-environment.png`确认原男女角色实际显示；诊断保存在actual-arena-camera.json。

完成后恢复SkillLab编辑状态，Error0；没有保存运行时场景修改。用户`.local/project.json`、F6/F7与本批开始的SHA逐字节一致；正式project.json仅改训练模型、敌人索敌半径、相机站立高度三处。无关材质/csproj/sln等不进入本批提交。

证据目录保存原始源码片段、保留哈希、构建/自检/Core/前端日志、现场状态和截图，不进入Git。完整13技能逐行为证据由既有共享轴/弹体、翻滚/炸弹、AI成长、输入FIFO、恢复区域/即时操作及原固定对照组成，不能把一个当前场景截图当全部证明。

本批完成历史环境/表现遗漏修复和当前文档同步。最终所有示例的网页完整流程、保存重开/导入/发布、全量回归及启动产物打包仍待统一验收；完整目标保持进行中。
