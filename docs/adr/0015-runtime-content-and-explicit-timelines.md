# ADR-0015 正式内容、显式Timeline与SO删除

日期：2026-11-14，验收更新2026-11-15。状态：当前V1–V4/TrainingCamp迁移已实施，Arena及产品扩展继续。关联N01–N10、M06、N03–N04。

## 当前代码证据

当前分支可执行的SO读取点是CombatRunner的V1–V4和TrainingCampRunner，均有无效或未配置时切回CodeCombatContent的路径。Config/DefinitionAssets.cs及EffectAssets/BehaviorTreeAssets为SO作者与Bake/cache；GenerateDefaultDatabase生成旧资产。Arena.unity为未跟踪旧快照，引用的脚本GUID在当前Assets源码不存在；不得宣称该场景已经有可迁移的ArenaBootstrap/Session。

## 设计

- 扩展现有SkillProjectDocument，不建立第二份人工维护的战斗配置。技能可以有显式timeline，包含稳定clip/payload身份、四类原Clip及effect入口节点ID列表。顺序列表支持重复项。时间轴内容由同一SkillEffectCompiler转换图中效果，网页参数与正式运行共用编译结果。
- 普通技能继续由施放参数生成Timeline；显式timeline保持独立runtimeId和精确时长/区间，禁止同时使用自动连发/引导/蓄力时序。原有Core TimelinePlayer仍为唯一时间推进者。
- 工程可选runtime段定义Combo边表、Actor蓝图、Motor、Cue和表现资源键。Config完成类型化映射/范围/引用/递归校验，Core工厂仅消费领域定义；角色差异使用组件组合、属性和已有BT配方，未知蓝图直接失败。
- 正式入口严格从StreamingAssets/Combat/project.json载入，整份编译通过后替换会话；缺失/坏JSON直接给出路径，不回退SO或代码。教学Demo中的独立CodeCombatContent只作样例，不能作为正式入口的后备。
- 网页选择正式工程后可编辑并显式发布完整内容到project.json；F6/F7仍从同一语义源输出依赖闭包。导出/复制/改名/删除必须保留显式Timeline入口和runtime引用，不让新模型绕过作者操作的身份管理。
- 完成原资产值/效果序列和真实行为对照后，删除SO作者类型、资产、Bake/cache和生成菜单，更新场景/测试/文档。原生Prefab/材质等表现资产保留，JSON仅存资源键，Unity用具名绑定或显式内置表现类型解析。

## 验收与剩余

保存旧资产解析快照作为迁移证据；独立JSON读写/坏引用/缺文件/共享状态和四类Clip边界/Combo不消耗失败输入验证；真实V1–V4/TrainingCamp PlayMode从磁盘运行，CLI同文件；扫描SO类型/资产GUID正常路径无引用。Arena缺失脚本及缺弹/木桶等当前未含能力仍需按完整清单补齐，不能用演示场景迁移代替最终交付。

执行记录见`../验证/2026-11-15-runtime-json-migration.md`：EditMode277/277、PlayMode35/35、网页24/24；删除30个SO源码/生成器文件与31个SO资产。显式Timeline和Combo领域类型已更名为TimelineDefinition/ComboTableDefinition，没有保留旧名shim。旧SO原值签名保存为测试fixture，新运行路径不读取它。ENG-001随SO删除关闭，ENG-002按领域拆分Demo并完整回归后关闭。
