# SkillLab 单工程、整组发布与 3D 试放验收

验收日期为 2026-11-22（Asia/Shanghai）。验收以 `0b6dd77` 为准备基底，覆盖单工程、发布、SkillLab 和 Arena 回归。设计口径见 [ADR-0053](../adr/0053-single-project-skilllab.md)。

独立验收原始文件位于 `C:\Users\Halo\AppData\Local\Temp\HaloCombat-single-skilllab-20261122-212043\evidence`。升级与清理证据位于 `C:\Users\Halo\AppData\Local\Temp\HaloCombat-upgrade-hack-20261122-214911`。以下证据名称相对于对应目录。

## 网页、草稿与并发控制

- 隔离网页保存并正式发布后，磁盘正式工程名称为「HaloCombat 正式战斗内容 · 隔离验收」，ID 为 `halo-combat-runtime`，保留 21 个技能和保存版本的 ETag。网页的完整工程下载原件为 `web-export-original.project.json`，包含同一工程 ID 与 21 个技能，SHA-256 为 `863867B78C07C1C2A61430FA31E6BCA85FB7C759FB3FD359D6BF3400214F1B00`。在原网页导入这份下载原件并合并后，技能数为 42。撤销合并后回到正式工程的 21 个技能。见 `formal-published.json`、`web-export-original.project.json`。
- 草稿条件保存返回 HTTP `200` 和新 ETag。两个使用过期 ETag 的请求（保存草稿、发布正式内容）都返回 `409`。缺 `If-Match` 的正式发布返回 `428`，检查点的正式文件哈希保持一致。使用新 ETag 正式发布返回 `200`，重新读取保留工程 ID 和 21 技能。见 `etag-verification.json`。
- `slot3` 为损坏 JSON，`slot4` 的根技能 `arena-double-shot` 在待发布工程中已删除。第一次发布返回 `409` 并列出两槽。确认前改变槽内容后，旧整组版本确认返回 `409`。使用新版本只确认一个失效槽也返回 `409`。三次拒绝前后各槽哈希一致。用新版本精确确认两槽后返回 `200`，只清空 `slot3` 与 `slot4`，新 `f6` 生效。见 `conflict-verification.json`、`slot3-corrupt-original.json`、`slot4-original.json`。
- **Chrome 修订版复验。** 编辑共享弹体后立即发布，以及重新载入磁盘后立即发布，两条路径的成功提示均独立显示，未被预览状态覆盖。坏槽确认弹窗列出槽 3，确认后清除了该槽。此次复验的浏览器控制台未见 Error 或 Warning。

## 整组发布与 Play Mode

正式 JSON 中 `arena-ammo-shot` 与 `arena-double-shot` 共用 `arena-normal-projectile`，其原始 `speed` 为 `6`。据此预期，改共享弹速后发布一个槽，应按同一编辑快照重建两个占用槽，保留未选槽的根技能，并在 Unity 自动切换一次。

| 操作 | 落盘与 Unity 结果 | 最后槽写入至首次观察的上限 | 采样间隔 |
| --- | --- | ---: | ---: |
| 共用弹速 `6 → 10`，选 `arena-double-shot` 发布槽 2 | 两槽都为 `10`。`f6` 仍为 `arena-ammo-shot`，`f7` 仍为 `arena-double-shot`。Unity 代次 `1 → 2`，两槽修订均匹配 | `210.1049 ms` | 约 `106 ms` |
| 共用弹速 `10 → 11`，选 `arena-double-shot` 发布槽 1 | 两槽都为 `11`。`f6` 换根技能，`f7` 保留原根技能，两包修订一致。Unity 代次 `2 → 3` | `267.1036 ms` | 约 `83 ms` |

两轮在 SkillLab 运行中自动生效，均在本机 1 秒验收线内。计时使用两槽中较晚的 `LastWriteTimeUtc` 和 Unity 首次观察到新代次的时间。文件写入时间可能早于事务提交完成，浏览器点击到落盘的时间未计入。见 `hot-run1.json`、`hot-run2.json` 与对应 `hot-*.txt`。

- **空槽启动。** 进入 Play Mode 后自动得到 `arena-ammo-shot`、`arena-returning-shot`、`arena-bomb-shot` 和 `arena-tracked-shot`，代次为 `1`，无加载错误。原始 Game View 捕获在四个技能上记录 `20/1/1/7` 次技能输入。原始捕获的窗口操作归属未确认。见 `unity-empty.json`、`default-four-slot-inputs.json`、`run-checkpoint.json`。
- **实体按键与 HUD 点击。** 隔离 Unity Game View 内，场地左键施放弹药射击，`Q` 施放正弦回旋弹，`R` 施放传送弹，单独按 `E` 施放弹跳与延迟炸弹，单独按小键盘 `1` 再次施放弹药射击。累计 `Casts=5`。左键点击静音 HUD 后，`PointerInWorld=false`，`Casts` 保持为 `5`。见 `stage3-inputs.json`。
- **零资源持续引导与 v1 回放。** `qa-held-spray` 的能量和弹药在施放前、持续引导及再次引导时皆为 `0`。三次开始请求为 `true/false/true`，记录到 2 次施放、10 个弹体、9 次命中、848 实际伤害。导出的 `formatVersion: 1` 原件包含 `project.preview.unlimitedAmmo: true`，重放对照 1979 个值，容差 `0.0001`，最大数值差 `0`。另一轮回放中发布新引导技能，回放保持原版本，25428 个值完全匹配，退出后自动载入刚发布的引导技能。见 `zero-resource-run.json`、`zero-resource-replay.json`、`zero-resource-reproduced.json`、`replay-during-publication.json`、`retest-summary.md`。
- **引导扣费配置复测。** 原件明确配置起手能量 `10`、弹药 `1`、每秒能量 `12`、每次脉冲能量 `2` 及冷却 `0.7 s`。零资源下两次成功引导累计生成 10 个弹体，两次之间的请求被拒绝，资源始终为 `0`。该 v1 原件重放也匹配 1979 个值，最大数值差 `0`。见 `explicit-cost-pulse-run.json`、`explicit-cost-pulse-replay.json`、`explicit-cost-pulse-reproduced.json`。
- **坏包保留。** 损坏隔离目录 `f7` 后，Unity 报告 `$.slots.f7` 解析失败。前后代次均为 `8`，会话修订、所选 `qa-held-spray` 和两槽修订保持一致。见 `unity-bad-publication.json`。
- **Arena 正式扣费。** 在正式 Arena Game View 内实际按左键施放，弹药 `60 → 59`，`Unlimited=false`。正式模式读取 `Combat/project.json`，无加载错误。见 `stage3-arena-lmb.json`、`arena-ammo.json`。

## 3D Game View 与音效

| 独立取证 | 覆盖与观察 |
| --- | --- |
| `skilllab-1080p.png`，1920×1080，原始布局 | 3D 网格、木桩、障碍、血条、四槽和 DPS 面板可见。相邻 `target-8`、`target-1` 的标签重叠，第四槽长技能名换行后底部裁切。 |
| `skilllab-final-1080p.png`、`skilllab-final-720p.png`，1920×1080、1280×720，修订布局 | 两张静态 Game View 中相邻木桩标签分开，第四槽长名以单行省略号收尾，四槽和 DPS HUD 在画面内。 |
| `skilllab-final-impact-720p.png`，1280×720，早期命中帧 | 显示 `80.0` 总伤害、`1` 次有效命中、近 5 秒 `16.0` DPS，并可见木桩命中闪光和伤害跳字。该帧的跳字与目标标签局部相叠。 |
| `final-label-fix-impact-720p-0.png` 至 `final-label-fix-impact-720p-3.png`，1280×720 | 修订后对 `target-7` 再次命中的四帧中，伤害数字 `80` 均与目标标签分开。 |
| `hud-hit-regions-720p.json` | 720p 下实测 HUD 坐标判定，`(70,75)`、`(1110,180)`、`(333,658)`、`(900,660)` 位于 HUD 内，场地点 `(600,200)` 位于 HUD 外。 |
| `audio-assets.json`、`skilllab-audio-feedback.txt`、`skilllab-pause-audio.txt` | 四个音频 Clip 和粒子贴图可加载，施放和命中时 `AudioSource.isPlaying` 为真。命中记录 7 个粒子与 1 个跳字。静音或暂停时音源停止，恢复时未补播。独立系统音频听感、连段确认音及范围爆发音的实际触发待补证。 |

跳字复验及最终输入复验的 Unity Console 查询均返回 0 条 Error。最后一轮结束后隔离 Editor 已关闭，原 Editor 与 8080 监听仍在。见 `unity-final-errors.json`、`stage3-unity-console.json`、`stage3-cleanup.json`。

## 构建、回归与旧数据清理

- Web 和 Host 构建完成。Web 有压缩后 chunk 大小提示，Host 为 0 警告和 0 错误。正式 JSON CLI 校验通过，包含 21 技能、10 个角色蓝图，`g1` 试放命中 3 次、实际伤害 22。CLI 的 16 个教学 Demo 全部完成。正式 JSON CLI 日志记录了 `NETSDK1138` 目标框架提示。见 `web-build.log`、`host-build.log`、`formal-json.log`、`teaching-all.log`。
- 独立只读检查确认 `SwordPractice` 场景、配置、相应 `.meta` 及实时引用已移除。四份旧槽和对应 `.meta`、本地 `.local/lab` 与 `workspace-default.txt` 均已移除。活跃目录中未找到两个获准清理的草稿 ID `47540ae3-6d8d-4e31-a34b-5f4a32d46926` 与 `52f3075a-2ea3-4ca7-bb9b-94a6b2163fd4`。见 `workspace-cleanup-readonly.txt`、`scratch-ids-readonly.txt`、`scene-reference-scan.txt`。
- 正式工程与唯一 Arena 草稿各保留 21 技能，独立核对的 49 份 JSON 各自与执行前基线逐文件 SHA-256 一致。23 技能历史根档与旧备份逐文件比对 28 项一致，历史索引 SHA-256 为 `9308818212B24E57552264BF1630B0F1797E4818BCEDEC54D665DBEA2BC30729`。隔离升级还验证了无草稿时不创建 Arena 草稿，指定外部数据目录时备份落入该目录，存档保持原哈希。跨目录 junction 被拒绝，外部及源数据哈希未变。见 `workspace-cleanup-readonly.txt`、`archive-vs-legacy-backup.txt`、`archived-before.sha256.txt`、`archived-after.sha256.txt`、`no-draft-assertions.txt`、`external-data-assertions.txt`、`junction-assertions.txt`。

## 尚未独立覆盖

第四槽悬停全名、角色移动和镜头缩放待独立验证。连段确认音和范围爆发音正在补测。独立听感仍缺系统音频或硬件取证。
