# ADR-0040：历史环境审计与相机表现恢复

日期：2026-11-15。状态：本批验收通过。关联：N01/N02/N05/N08/N10、M06、O06/O07。

技能/时间轴/弹体/Buff/区域/输入已有794值历史对照。本轮继续从`fae78727dd7a6d6f8c4f8dd7c2a6c8814183058a`的实际角色、规则、Motor、View表与相机源码核对；固定为独立测试夹具OriginalArenaEnvironment.json，84个环境/表现基线值不进入运行路径。包括三种角色的团队/属性/生成成长/Prefab GUID、刷怪/清尸/木桶周期、Motor、原站立/倒地视点、弹簧与命中冲量。

发现并修复三项遗漏：训练靶应使用原敌人骨骼模型，不能继续使用教学stake方块；正式敌人的AcquireRadius为20；相机不能把固定0.75米视点直接贴在玩家逻辑位置。正式JSON分别恢复arena-enemy-view、20和站立1.6米；其他项目/已发布JSON的cameraFocusHeight默认值不暗改。

新增ArenaCameraMotion属于无Unity依赖的表现模块：首次定位直接对齐，之后按`1-exp(-Hz*dt)`平滑跟随，站立1.6/倒地或死亡0.55，原Light/ComboConfirm/Heavy/Finisher冲量0.2/0.35/0.55/0.8。较弱反馈不覆盖较强反馈，冲量按渲染时间线性衰减。Arena绑定只接收本地玩家实际发出的EvDamage.Result.Feedback；不从后来施法或目标当前状态猜测。相机偏移、频率、shake0.045、FOV冲量2.5和相位70/61保持原源码。

ArenaPresentation只把只读角色姿态/Tag及渲染时间交给相机状态，不修改Actor、ActorTime、Timeline或战斗Clock。逻辑暂停/顿帧时表现可继续收敛/衰减，与原LateTick一致；真实动作和伤害仍由Core推进。每个候选表现绑定独占相机实例和伤害订阅，重载失败保留旧绑定，重开成功后退订旧世界。

JSON新增可选`cameraDownedFocusHeight`默认0.55、`cameraSpringHz`默认8（0显式立即跟随）、`cameraShakeEnabled`默认true。默认值省略，保持旧发布载荷稳定；校验有限数值/范围，DTO独立转换到ArenaDefinition。没有引入相机配置SO、兼容shim或双写路径。网页正式runtime JSON可编辑这些字段，类型模型同步；Unity适配器Sample改为显式渲染delta和时间，现有调用/测试一并更新。

原始资源目录没有音频文件，继续明确说明，不凭空生成替代音频。本批没有修改既有美术文件，也没有覆盖用户工程和两槽；原角色/资源GUID与目录由既有资源测试及新增环境夹具验证。域模型活动集合仍为None/Root/Attack/Hit/Dead/Knockdown，ComboComp沿当前技能/输入/标签查表，没有恢复技能图解释器或独立阶段机。蓄力/引导/操作和诊断上下文的新增语义分别由既有ADR定义。

当前README、节点映射、角色资源说明与新AGENTS统一JSON内容源、FIFO输入、53模板和验收边界；历史文档保留当时记录。运行和测试证据见[环境与相机验收](../验证/2026-11-15-arena-environment.md)。最终统一流程、全量回归与可启动产物交付继续，不把本批完成当完整目标达成。
