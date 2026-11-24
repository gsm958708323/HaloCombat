# ADR-0054 连招工作台、统一条件模型与分支独立取消窗

日期：2026-11-23。状态：已采纳并实施。取代 ADR-0048 与 ADR-0052 的旧连招界面与 `purpose` / `requiredTags` 字段设计。

## 背景

既有连招编辑器存在三类结构性限制：
1. **运行时表达不足**：`ComboEntry` 仅支持正向 `requiredTags` 与全局单一 `CommonTags.Cancel (1001)`，无法表达未命中挥空、Buff 层数门槛、能量/弹药条件，也无法在同一动作上区分「闪避早取消」「普通连段」「停顿延迟变招」等独立取消时机。
2. **元数据混杂**：`RuntimeComboDocument.Purpose` 不参与战斗核求值，其中 `entry`/`transition` 与 `preSkills.Length == 0` 冗余，仅 `cancel` 用于前端画布折叠显示，导致运行时 JSON 夹带作者视图状态。
3. **作者体验受限**：旧图禁用节点拖拽与连线编辑、规则表单退化为手写原始 JSON 文本框、时间轴无法直接拖拽窗口且选边时展示目标而非前置技能的时间轴、试放仅提供下拉框单发输入。

## 决策

1. **统一条件模型并移除冗余字段**：
   - 硬删除 `RuntimeComboDocument.Purpose` 与 `RequiredTags`（及 `ComboEntry.RequiredTags`），同步迁移正式 `Assets/StreamingAssets/Combat/runtime/combos.json` 与工作工程 `Tools/SkillLab/.local/arena/runtime/combos.json`。
   - 新增扁平 AND 条件数组 `conditions: ComboCondition[]`，每项支持 `negate?: bool` 与三种原子条件：
     - `tag`：`tag`（正整数 TagId）
     - `buff`：`buff`（Buff 定义 ID）、`op`（`gte | eq | lt`）、`value`（非负整数层数）
     - `resource`：`resource`（`energy | ammo`）、`op`（`gte | lte`）、`value`（非负数值）
   - 保持优先级消歧不变量：相同 `input` 且 `preSkills` 有重叠的两条规则必须拥有不同 `priority`。
2. **分支独立取消窗**：
   - `TimelineClip`（仅 `kind == "cancel"`）增加可选 `channel?: string`（空/省略为默认通用窗，继续沿用 `CommonTags.Cancel` 保证既有逻辑与直接施放不变；非空命名轨由 `SkillDirectorComp` 维护活跃频道计数，在 `Stop`/`Replace`/`Detach` 时完整释放）。
   - `ComboEntry` 增加可选 `window: { mode: "default" | "channel" | "range" | "anytime", channel?, start?, end? }`（省略为 `default`）。
   - `ComboComp.TryResolveCurrent` 对每个匹配 `(currentSkill, input)` 的候选独立评估窗口与条件：若所有匹配前置的候选均未开启窗口，上报 `phase = "waiting"` 并保留缓冲输入；若窗口开启且条件满足，返回 `WindowVerified = true`。
   - `SkillCastComp.TryCast` 新增可选参数 `cancelWindowVerified = false`，仅由通过 `ComboComp` 验窗的驱动路径传入 `true` 以放行命名轨/自定义时段/任意时刻取消，绝不绕过死亡、硬直、倒地、沉默、BlockSkill、Hitstop、冷却、能量或弹药守卫。
3. **编辑元数据与分层工作台**：
   - 公共取消栏的折叠收纳改存纯编辑元数据 `editor.comboCancelRuleIds`（导出运行时包时与 `editor` 一并剔除）；一技能一主卡的自由位置存 `editor.comboPositions`（含 `$idle` 起手入口）。
   - 画布升级为「空闲起手入口 + 一技能一主卡自由可编辑 DAG + 顶部自动连招链聚焦 + 顶部公共取消栏 + 全表规则总览」，下方提供宽幅可折叠拖拽交互时间轴坞（选边 `A → B` 自动展示前置技能 `A` 的轴），右侧提供实时结构化检查器与同键优先级排序栈，左侧升级为彩色按键手柄栏与键盘实时搓招。

## 边界与影响

- 因 `SkillProjectJson` 启用 `MissingMemberHandling.Error`，硬删除 `purpose` 与 `requiredTags` 后，旧导出包中若残留上述字段将被严格拒绝，需使用新格式。
- `conditions` 中的 `buff` 引用纳入 `SkillProjectStructure` 校验、Buff 重命名/删除影响分析与工程合并重映射。
- `ComboComp` 条件求值严格限定于施法者自身组件（`TagComp`、`BuffComp`、`EnergyComp`、`AmmoComp`、`SkillDirectorComp`），不引入目标侧不确定依赖。
