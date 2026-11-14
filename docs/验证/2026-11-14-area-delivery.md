# 2026-11-14 目标、范围与持续区域验收

分支 `codex/web-skill-editor-json`。范围为 ADR-0006，完整编辑器目标仍在开发。

## 执行结果

| 检查 | 最终结果 |
| --- | --- |
| `dotnet build Combat.csproj` | 通过；原 net6.0 的 NETSDK1138 提示保留，无编译错误 |
| Core regress | `ALL S1+S2 REGRESSION PASSED`，含 Aura 来源租约/死亡清理与现有跨系统演示 |
| 宿主 build / self-test | 通过；普通射击、蓄力、爆裂弹 240/3、持续区 300/15、目标打击 80/1 |
| 网页 TypeScript / Vite | 构建通过；最终产物 `index-CDSFDGyw.js` |
| Unity 编译 | MCP 编译后 ready_for_tools=true，非编译/非 Domain Reload，Error 日志 0 |
| Unity EditMode | job `56792d0e946544dd8aa2f909e20f1e57`，49/49 Passed |
| Unity PlayMode | job `64451b7c813e442caad4726b7dc8fe7a`，9/9 Passed，包含既有教学场景、发布、引导/蓄力和新增范围/区域 |
| 浏览器爆裂弹 | 从“爆裂弹”模板新建，半径 4 实际命中 3 个目标/240 伤害；改半径 1 后 1 个/80，保存并发布 F6 |
| 浏览器持续区 | 从“地面持续伤害”模板新建，5 次脉冲共 15 次伤害投递/300，区域在 1.10s（0.10s 施放延迟 + 1s 寿命）自然到期，保存并发布 F7 |
| 实际发布跨端对照 | 宿主与 MCP 内调用的 SkillLabRunner 从浏览器实际发布的两槽文件载入，各推进 80 步；F6 均 damage=80/hits=1/projectiles=1/areas=0，F7 均 damage=300/hits=15/projectiles=0/areas=1/liveAreas=0；time=1.5999999、energy=40 |

跨端对照属于 Editor 内真实适配器和文件读取证据；PlayMode 行为与圆圈/持续区视觉由独立场景测试证明，不混称为手动按键操作。

## 关键覆盖

- 命中点半径、目标上限、目标/自身选择、缺失中心无回退，最大支持的配置范围内显式校验错误路径。
- 64 个目标不被静态 32 缓冲截断；友方、死亡、投射物与 AoE 被排除。嵌套圆查询、跨世界重入脉冲、注册表快照不串状态。
- 瞬时范围不生成区域实体；持续区真实实体、半径/剩余时间、生成脉冲、关闭生成脉冲、寿命终点不多打一发，0.001s 间隔在 0.1s 内补齐 100 次触发。
- 蓄力倍率保存到区域实例，每次脉冲正确使用；同一节点被重复连接仍生成两次，而非编译时去重或重复注册报错；禁用分支不运行。
- Enter/Exit/Stay 与自然到期顺序；目标离开后重新进入；Owner 死亡只清理 Exit/实例，不误报 Expire；原 Aura 多来源叠层清理仍由 Core 回归覆盖。
- Unity 通过快照显示真实爆炸圆圈和区域大小，场景测试检查圆圈 LineRenderer 与半径 4 对应直径 8。

## 本机验收产物

位于 `Tools/SkillLab/.local/verification`：`area-editmode.json`、`area-playmode.json`、`host-published-area-f6.json`、`host-published-area-f7.json`、`unity-published-areas.json`、`core-regress-area.log`、`area-publication.jpg`。均为忽略的本机输出。

尚待完成近战/位移/射线/召唤、Buff 和更多投射物行为、完整工程交互、正式 JSON 内容迁移与 SO 删除、全部示例和视频完整视觉对照。新目标/区域节点的完成不代表这些范围已交付。
