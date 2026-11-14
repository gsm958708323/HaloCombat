# 2026-11-14 障碍、射线与弹体终结验收

分支 `codex/web-skill-editor-json`，对应 ADR-0008。完整编辑器仍在开发，本批没有完成召唤/Buff/更多弹道及 SO 迁移。

## 最终结果

| 检查 | 证据 |
| --- | --- |
| Core build | `dotnet build Combat.csproj` 通过，原 net6.0 的 NETSDK1138 提示保留，无编译错误 |
| Core regress | `ALL S1+S2 REGRESSION PASSED` |
| 宿主 build / self-test | 通过；既有射击/蓄力/区域/动作和新增墙阻挡、闪现前置净空均通过 |
| 前端 | TypeScript/Vite 成功，最终 `index-DlNqnocI.js` |
| Unity 编译 | MCP 刷新恢复后 ready_for_tools=true、非编译/非 Domain Reload，Error 日志 0 |
| Unity EditMode | job `765a93c0ea084ffea56c37fa5554de5d`，86/86 Passed |
| Unity PlayMode | job `7272412a928f4e6e8127624fad88e0ba`，15/15 Passed，含既有场景与全部 SkillLab 测试 |
| 浏览器 | 从贯穿射线模板新建，将 3 个目标布到同一水平线；无墙 240/3，添加矩形并将 X 改成 2 后 160/2；关闭穿透 80/1；移除障碍恢复 240/3，撤销恢复墙后重跑 160/2，保存并发布 F7，同步 F6 场景 |
| 实际射线显示 | 暂停在触发后，SVG 线终点 x=1.5999999、全宽 0.3，墙为 X=2/宽0.5；与核心扫掠半宽接触一致 |
| 实际发布跨端 | 宿主与 MCP 内 SkillLabRunner 各读取两槽发布包，10 步保存射线终点，再推进70步；F6 为80/1/1弹，F7 为160/2/0弹，time=1.5999999、energy=40、障碍1个、F7早期终点1.5999999 全部一致 |

测试首次启动时遇到编译重载，未返回 job ID；核对 Editor tests.is_running=false/current_job_id=null 后才重新启动。上表只记录最终有明确 job 和终态的测试，未把断连当作通过。

## 核心覆盖

- 薄墙连续圆扫掠、真实圆角、最近障碍排序；接触面向外/切向可移动，向内阻挡；障碍定义独立快照与非法尺寸检查。
- 普通移动和速度20/1000的冲刺均停在角色净空处，无墙后伤害；近战不可穿墙。落点被占闪现扣费前拒绝，合法落点可越过中间墙并精确到点。
- 射线按真实接触距离而不是注册顺序选最近目标；穿透/目标预算/宽度、墙前停止、首目标提前停止不触发后方墙；wall/complete 事件顺序与无命中完成；半蓄力2倍射线实际伤害。
- 1000米/秒弹体不越过目标或薄墙，枪口前移不会把弹体生到墙后；独立range/lifetime到点、不同原因不误触发、每原因一次再通用终结、命中伤害先于结束效果。
- 出生重叠、重复障碍身份、非正尺寸和被阻 Place 均有字段路径；Unity 障碍/射线可视对象与核心终点、移动/冲刺/闪现结果一致。

## 本机产物与范围

位于 `Tools/SkillLab/.local/verification`：`collision-editmode.json`、`collision-playmode.json`、`host-published-collision-f6.json` / `f7.json`、`unity-published-collision.json`、`core-regress-collision.log`、`ray-obstacle-publication.jpg`。产物被忽略。跨端对照为 Editor 内适配器调用，PlayMode 场景行为由独立测试证明。

当前障碍为二维轴对齐实心矩形、移动不做滑墙；攻击目标按中心与攻击宽度结算，角色 collisionRadius 仅用于移动净空。范围圆 AoE/爆炸没有新增视线阻挡规则，依 ADR-0006 运行。Owner 清理为回收，不触发自然寿命原因包。召唤、Buff、多重/连锁/分裂与其他弹道、完整工程交互、正式内容 JSON 迁移/SO 删除及视频整体视觉对照仍保留原范围。
