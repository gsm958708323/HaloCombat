# ADR-0053 SkillLab 单工程与自动试放

日期为 2026-11-22。

## 工程与发布

网页只打开 `Tools/SkillLab/.local/arena/project.json` 及其分片。首次使用从正式 `Assets/StreamingAssets/Combat/project.json` 初始化。导入合并到当前工程，保留工程 ID。原 23 技能工程的 `.local/project.json` 及 `.local/backups/legacy-project` 原地保留为历史档。移除切换、新建、跨工作区复制和手动同步入口。

- **发布正式内容。** 先用草稿 ETag 和 `If-Match` 保存，再用保存返回的 ETag 调用 `/api/runtime/publish`。宿主核对磁盘草稿修订并校验完整运行配置，再更新正式内容。Arena 保留正式和木桩入口、`F5` 重载与 `F6/F7` 试放槽。移除 `SwordPractice` 场景、菜单及专属配置。
- **发布到槽位。** `/api/publish/{slot}` 使用点击时的同一份编辑快照。所选槽绑定当前技能，其余占用槽按已发布的根技能 ID 重建。完整组通过校验后，在目录锁下事务提交，写入失败时回滚。延用 `f6.json`、`f7.json`、`slot3.json` 和 `slot4.json`。发现其他槽有损坏、来源不符或技能已删除时，返回具体失效槽及整组 ETag。确认重试必须同时携带 `If-Match` 和与该组精确匹配的失效槽列表。读取到不同修订时须重新检查。

## Unity 试放与表现

- SkillLab 进入 Play Mode 后自动读取发布组，运行中继续检查。后台读取与宿主提交使用同一把目录锁。在 Unity 主线程验证候选会话和资源，成功后切换并重置试放输入。失败继续使用上一组有效会话，HUD 显示原因。空槽或首次载入失败且没有有效会话时，从正式 `Fire1–Fire4` 的起手绑定解析四招并使用默认木桩布阵。`F5` 可手动重载，退出回放时恢复最近的有效发布。
- SkillLab 只在创建试放会话时复制内存里的 `PreviewDocument` 并启用已有的 `UnlimitedAmmo` 开关。玩家能量和弹药免扣，包含持续引导。冷却、施放状态和 Buff 层数仍由战斗核结算。v1 输入回放将实际建局使用的 Preview 写入 `project.preview`。正式 Arena 继续按正式配置消耗资源。
- 3D 训练场的施放、命中、范围爆发、连段提示、伤害跳字与 DPS 消费战斗核实际事件。表现层不推算伤害或修改结算。音效及粒子贴图使用 Kenney 的 CC0 素材，授权页和实际取件位置见[素材授权记录](../../Assets/Resources/SkillLab/LICENSES.md)。
