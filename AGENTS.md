# HaloCombat 工程约定

完整需求见 `docs/网页技能编辑器功能清单.md`，当前事实见 `docs/实施进度.md`。网页参考原视频功能，允许按项目可用性适配；装备、公式编辑器、独立地图、冒险不在范围内。

- 正式内容入口是 `Assets/StreamingAssets/Combat/project.json`索引，功能数据在同目录skills/projectiles/areas/buffs/timelines/summonBlueprints/runtime及preview文件中。示例索引为 `Content/SkillLab/project.json`；磁盘读取/保存统一走SkillProjectFiles，不把索引当完整文档解析，不用教学CodeCombatContent回退。
- 用户工作工程为 `Tools/SkillLab/.local/arena/project.json` 与 `.local/lab/project.json` 索引及引用模块，`f6.json`、`f7.json`、`slot3.json`、`slot4.json` 为独立用户发布快照。测试使用独立临时工程和目录，保留用户数据；不要把槽文件、用户分片或无关生成文件混入提交。协议单文件导入/导出属于可携带文件契约，不能与磁盘分片人工双写。
- `Combat.Core`持有领域定义和实例状态；`Combat.Config/Json`负责协议校验、独立转换及目录；网页负责作者操作、输入与快照显示，预览宿主和Unity执行同一战斗核。表现逻辑属于`Combat.Presentation`/`Combat.Unity`。
- 内部重构直接删除过时实现；公开JSON契约的默认值、身份、版本和修订需独立评估。不得恢复SO内容作者链或双写配置。
- 先查已有实现和依赖，优先组合与明确接口；单个源码文件不超过500行。非显然决策、限制和风险写ADR，临时问题关联可追踪任务，不留下无上下文TODO。
- 原Prefab、材质、动画和纹理通过明确资源键保留，迁移资源时保持GUID。缺失音频只能明确记录，不能用空键声称音效已交付。
- Unity 优先使用 Coplay Unity MCP 检查编译，在实际场景中进行 Play Mode 操作，并检查 Error 日志。场景/表现证明不能用.NET构建替代；MCP域重载短暂断连先核对同一会话，不因一次超时重启Editor。
- 开发时做相关检查，完整批次结束集中验收；新改动或失败才扩大或重跑。JSON/输入改动以实际文件验证网页与Unity，下载原件不能用手写近似配置替代。
- 教学批量运行使用 `dotnet run --project Combat.csproj -- all`。正式内容校验与试放使用 `dotnet run --project Combat.csproj -- json Assets/StreamingAssets/Combat/project.json`。网页在 `Tools/SkillLab/Web` 执行 `npm run build`，预览、发布和回放用独立临时工程验证实际路径。Unity 检查经 MCP 执行。
- 重要节点验证后本地Git提交，不自动推送；排除无关csproj/sln、材质重序列化、`.gitignore`和`.local/verification`证据文件。

使用入口见`Tools/SkillLab/README.md`，验收记录位于`docs/验证`。历史记录按当时状态保存，不把一个样例通过当成完整交付。
