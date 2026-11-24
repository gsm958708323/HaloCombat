# 2026-11-14 蓄力与预览版本验收

分支 `codex/web-skill-editor-json`。本批对应 ADR-0005，不表示完整编辑器/旧 SO 迁移已交付。

## 最终执行结果

| 检查 | 结果与证据 |
| --- | --- |
| Core build | `dotnet build Combat.csproj` 通过；原有 net6.0 NETSDK1138 提示保留，0 编译错误 |
| Core regress | `ALL S1+S2 REGRESSION PASSED`，日志 `Tools/SkillLab/.local/verification/core-regress-charge.log` |
| 宿主 build / self-test | 通过；半蓄力经真实投射物造成 160 伤害、初始 50，扣启动 10 和蓄力 2.4 后剩余 37.6 |
| 网页构建 | TypeScript/Vite 成功；最终产物 `index-C9ixf5NN.js` |
| Unity 编译 | MCP 刷新到 ready_for_tools=true、非编译/非 Domain Reload；当前 Error 日志 0 |
| Unity EditMode | job `00cfeff3c2e44b0c8c41f8bb583fe4be`，31/31 Passed |
| Unity PlayMode | job `22b02a4c9ee14f7cb2e127893f26b329`，7/7 Passed，含既有教学场景和所有 SkillLab 测试 |
| 浏览器真实流程 | 新建“蓄力验证”→切蓄力→改最大蓄力为 0.6s、暴击 0→资源初值 50/上限 100/恢复 0→按住空格→自动释放 3 倍、约 240 伤害、1 枚/1 命中，剩余 40→保存→发布 F7 |
| 实际发布文件跨端对照 | 宿主和 Unity 适配器读取浏览器保存并发布的同一技能，自动蓄力推进 80 步，均为 time=1.5999999、energy=40、1 枚/1 命中/1 次施放，伤害约 240；float 序列化输出分别 239.99998 和 239.999985，差异 < 0.00001 |
| 预览版本与坏配置 | 技能切换后立即观察施放 disabled，出现“当前配置已应用”后 enabled；输入初始能量 101 的 JSON，应用返回 `$.preview.initialEnergy`，原工程保持初值 50，坏文本保留可修复；最终浏览器重跑仍正确造成约 240 伤害 |

## 行为覆盖

EditMode 覆盖提前松开无伤害不退款，0.2/0.6/1 秒松开分别 80/160/240 伤害，满蓄自动释放一次/手动满蓄等待，释放后延迟与方向锁定，受击/死亡/取消输入不发射，Hitstop 冻结保持时间与扣能，不足时取消；旧弹体保存自己的倍率，跨模拟共享定义不共享蓄力状态。2 倍基础伤害减 20 防御的结果为 140。

PlayMode 从真实磁盘文件和场景加载，验证独立初值、重复按下守卫、蓄力进度、有效释放伤害/持续耗费以及 Unity 适配器的输入取消。自动满蓄释放事件在投射物生成之前记录；所有正式伤害经过统一管线。

## 本机产物

原始结果：`.local/verification/charge-editmode.json`、`charge-playmode.json`、`host-published-charge.json`、`unity-published-charge.json`；截图 `charge-ready.jpg` 与 `charge-publication.jpg`，均位于 `Tools/SkillLab` 下。本机产物被 Git 忽略。

完整目标尚余更多传递/效果/行为节点、编辑工程交互、所有验收例子、旧 SO 路径迁移删除和完整视频对照。蓄力字段为明确的项目规则，不能据此声称原工具未公开字段已经完全复刻。
