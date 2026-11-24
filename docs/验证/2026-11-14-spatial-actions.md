# 2026-11-14 近战与位移验收

分支 `codex/web-skill-editor-json`。本批对应 ADR-0007；完整目标仍在进行，障碍落点不算完成。

## 执行结果

| 检查 | 证据 |
| --- | --- |
| Core build / regress | build 通过，原 net6.0 提示保留；`ALL S1+S2 REGRESSION PASSED` |
| 宿主 build / self-test | 通过；刺击 80/1、扇形/旋风 160/2、冲刺 160/2 且从 -6 到 6、闪现 240/3 且到 (0,0)，完成后判定关闭 |
| 前端 build | TypeScript/Vite 通过，最终 `index-ZsX2K3bM.js` |
| Unity 编译与日志 | MCP 编译后 ready_for_tools=true、非编译/非 Domain Reload，Error 0 |
| Unity EditMode | job `ac8d337562484bc2891bcb61e4f3b18e`，64/64 Passed |
| Unity PlayMode | job `94786089a61b48efaed9b4486be8a39c`，12/12 Passed，含既有场景与全部 SkillLab 场景检查 |
| 浏览器刺击 | 从模板新建“刺击验证”、攻击时长改为 1s，实际 80/1；暂停时保留活动矩形，按打断后同时间点记录 Hit/判定关闭，保存并发布 F6 |
| 浏览器冲刺 | 从模板创建，160/2，实际 SVG 施法者位姿从 x=-6 到 x=6.000001，结束后判定关闭，已保存 |
| 浏览器闪现 | 从模板创建，0.2s 落点范围结算共 240/3，实际施法者 SVG 位姿为 translate(0,0)，保存并发布 F7 |
| 实际发布文件跨端对照 | 宿主与 MCP 内执行的 Unity SkillLabRunner 各载入浏览器两槽文件、施放后 80 步；F6 damage=80/hits=1/casterX=-6，F7 damage=240/hits=3/casterX=0；time=1.5999999、energy=40、volumes=0 一致 |

跨端对照是 Editor 内真实适配器调用，不等同于手动键盘操作；PlayMode 场景和判定 LineRenderer 由独立测试覆盖。

## 关键覆盖

- 刺击宽度/方向、扇形角度与半径、360° 后方目标、前摇前无伤害、新目标进入有效期、同实例去重与总目标上限。
- 冲刺速度 20 与 1000 时同样扫掠两目标、总位移恰好 12m；中途受击后位置与命中停止，判定归零。
- 超射程闪现拒绝且不扣能量/不开冷却；有效闪现仅命中落点、不中途径目标；延迟期间目标移出射程时不再闪现；前摇中受击清理待触发项。
- 连发每段守卫先于动作启动；重复近战实例分别命中、引导松开关闭全部活动判定，半蓄力近战造成 160 伤害。
- 大步同时跨过 Move Clip 起止点时只积分区间交集，没有整步额外位移；事件中的判定生命周期和 Unity 可见 LineRenderer 在打断后都消失。

## 回归发现与修正

旧 EnemyAiDemo 的 `minDistanceToHome` 只在 AdvanceFor 的末尾 details 回调采样，因此新时间修正后末帧巡逻位置 -0.600000… 被误认成“从未回 Home”。将采样移入每次 Step，原 0.6m 阈值与 150 步均保留；实际末帧 distance=0.60、整段 min=0.00，原断言通过，未修改 AI 规则。

## 本机产物与范围

位于 `Tools/SkillLab/.local/verification`：`spatial-editmode.json`、`spatial-playmode.json`、`host-published-spatial-f6.json` / `f7.json`、`unity-published-spatial.json`、`core-build-spatial.log`、`core-regress-spatial.log`、`melee-active.jpg`、`blink-publication.jpg`，均被忽略。

当前几何按实体中心进行判定；二维平面尚无障碍体，因此不声称碰撞阻挡/地形落点验证完成。射线、召唤、Buff、更多弹体行为、完整工程交互、SO 迁移删除和视频整体视觉验收仍按完整清单推进。
