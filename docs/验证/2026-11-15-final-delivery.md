# SkillLab最终交付产物

日期：2026-11-15。完整要求到证据见[最终验收](../最终验收.md)。本记录在产物验收后提交，归档内源码以manifest的sourceRevision为准。

## 可运行产物

- 文件：`Tools/SkillLab/.local/releases/SkillLab-1.0.0-final.zip`。
- 大小：51,136,874字节。
- SHA256：`819f74c9e72fde7ba4988799b3b399ca56a91385ac7582d89d1a27ffa8042853`。
- 来源提交：`228b66e80a3f27e255ae9d339be2d322a6d173c7`，manifest的`sourceDirty=false`。
- 平台：Windows x64，self-contained，含.NET运行时、网页、53模板、正式JSON、协议/映射/ADR/验收与启动说明。

ZIP解压后逐一核验434份manifest文件，全部匹配。确认包含coreclr.dll及pagehide修正的前端构建，不含用户工作工程或F6/F7。包外`.zip.sha256`与归档重新计算一致。

从系统Temp工作目录启动解压所得EXE，使用4194和独立数据/发布目录：HTTP200、53模板、带ETag保存/加载均通过。普通射击100步80伤害/1命中/1弹体；持续引导100步21弹体/11命中，快照HTTP正常，松开后停止。整数formatVersion=1.5返回400。验证结束仅停止本次启动的包进程。

证据：`.local/verification/final-delivery/package-final-smoke.json`、`delivery-checks.json`和`final-package-host.log`。产物断言通过并写出smoke证据后，外围命令曾误把PowerShell脚本的遗留LASTEXITCODE当成脚本结果而报错；随后独立读取全部断言、核验归档和保存文件，确认通过，无需产品修复或重复全量测试。

## 最后运行状态

源码4192首页HTTP200；普通射击及持续引导真实运行通过。Unity MCP确认SkillLab编辑态、未编译中、Error日志0。用户`.local/project.json`、F6/F7、正式project.json全部匹配保留哈希。

最终Unity EditMode796/796、PlayMode100/100、前端100/100、宿主自检/Core回归/构建通过。54份模板/前置记录在Unity重放94917值匹配；最终网页实际下载及输入原件在Unity加载/比较通过。最后会话回收修正另在仅余1个会话槽时实际刷新4次均就绪，已运行前端检查和构建。

## 启动

源码运行`./Tools/SkillLab/Start.ps1`，浏览器打开`http://127.0.0.1:4192/`。独立包解压后双击Run.cmd；端口已占用则运行`./Start.ps1 -Port 4193`。网页选择模板后施放并播放。Unity打开SkillLab点击Play，网页发布槽后按F5重载、F6/F7施放。

独立包提供网页运行预览；Unity游戏与原Prefab/动画/材质随源工程交付。用户允许项目适配；装备、公式编辑器、独立地图、冒险已排除。原隐藏字段互通未宣称，原素材未提供音频，不把绑定机制当成恢复了原音效。
