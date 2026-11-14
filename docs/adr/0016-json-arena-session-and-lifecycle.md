# ADR-0016 JSON Arena会话与直接入口

日期：2026-11-15。状态：已实施，验收见`docs/验证/2026-11-15-json-arena.md`。关联N01/N02/N06、M01/M02、O06/O07。

## 依据与当前增量

当前Arena.unity为未跟踪旧场景，引用缺失脚本。只读检查本地Git历史找到fae7872的BuffArenaSession/InputSource、ArenaRules/Presentation及Actor资产。原规则为10敌人/10秒补齐、5秒清尸、种子1，输入Fire1–5、Roll、Homing、Monkey，鼠标1–5与数字1–5、Space/Q/E，F5重开。历史源码只作为行为/值证据，不恢复SO或整套旧管理器。

本增量恢复直接进入、移动/瞄准/技能、敌人补齐、训练模式、暂停、死亡/重开、清理、JSON重载和F6/F7发布槽；后续ADR-0017补上了通用Ammo/换弹/传送弹重施放。历史13技能、木桶及资源原样迁移继续作为后续范围，不能把入口恢复宣称为完整旧玩法复刻。

## 职责与数据流

- runtime.arena保存角色引用、规则、边界/碰撞、输入→Combo令牌绑定和相机数值，独立DTO校验后映射到Core ArenaDefinition；不是地图/冒险编辑页面。相机保留历史距离/高度/FOV，新增0.75米focusHeight适配当前具名内置角色模型，后续美术恢复仍需独立对照。
- ArenaSession位于Config/Json，与宿主/CLI共用CombatWorld与RuntimeActorFactory；只持有一局实体、输入和刷怪/清理计时。Unity ArenaBootstrap负责磁盘、固定步钟、采样、PresentationRouter和HUD/相机。
- 先构建候选内容、会话和独立隐藏表现根，绑定事件后Start生成玩家并验证训练落点，成功才替换旧会话。World.PublishSpawn只能在位置/属性设置完毕后发出；读取/校验/Start失败均释放候选并保留旧会话。重开退订、释放表现、Shutdown旧World。
- 显式按下令牌进入原InputBuffer/Combo/SkillCastComp，不绕过守卫；保持主攻击单独保存，不挤占显式按键。失败输入不消费。输入优先级按历史Roll/Fire4/Monkey/Homing/Fire5/Fire3/Fire2/Fire1，同帧最多3项。
- 按住主攻击只在当前动作完成后再尝试根Combo；新的按下事件才可走取消边，避免持续输入截断自己的延迟Payload。主攻击和发布槽记录成功CastId，只有对应输入释放才能结束该次保持技能；失败槽松开不能取消另一个槽。暂停/失焦则统一取消。
- 同一固定步使用运动/瞄准输入。持续施法松开正常Release，失焦/暂停Cancel，不把取消当蓄力释放。暂停/死亡冻结战斗推进；清尸用独立wall time，死亡后继续回收且不执行伤害/刷怪。
- 发布槽来自同正式工程，合并定义时校验身份/版本，坏槽不替换旧会话。F5显式重载，F6/F7仍走统一施放守卫。
- 表现仅消费真实事件和世界状态，不拥有伤害。事件退订和ViewRegistry完整EntityId匹配作为多局隔离条件；不能用旧代死亡事件影响复用索引的新对象。
- 工程合并保持现有Arena；导入首个Arena时重映射角色与Combo输入令牌，防止同名Attack落入目的工程旧根边。敌人数只计有Health的敌方角色，不计弹体/区域/召唤体；随机128次仍无净空时本轮停止补齐，等待下一周期重试，不越过障碍强行生成。

## 验收

验证JSON缺引用/数值/出生净空、固定输入和随机重现、零逻辑步保留按下、连发/引导松开/取消、失败资源/冷却、首波与周期补齐、训练不刷怪、死亡冻结与wall清尸、重开/失败重载保留、F6/F7真实文件和资源释放。MCP编译、EditMode/PlayMode、Error日志以及真实Arena场景画面作为证据；重要节点通过后单独Git提交。
