# ADR-0010：共享 Buff 定义与生命周期效果图

日期：2026-11-14。状态：已实施并完成本批验收，见 [Buff 验收](../验证/2026-11-14-buffs.md)。原视频仅提及 Buff，未公开字段；本契约沿用 HaloCombat 的领域语义，不宣称是作者隐藏格式。

## 作者边界

工程增加 `buffs` 定义：稳定字符串身份、正数运行身份、名称、持续时间/永久、周期、叠层策略/上限、互斥组、授予 Tag 和属性 Modifier。每个 Buff 附带 Nodes/Edges，唯一 `buff-events` 根提供 apply/stack/period/expire/hurt/cast/removed 端口，其余效果复用技能图编译器。生命周期图不能隐式安排角色近战/冲刺动作。

技能的 buff 节点只引用 Buff ID、层数、目标/自身；dispel 节点按 Buff/互斥组/Tag/当前来源筛选，可限制实例数。Modifier 每实例只挂载一次，不随叠层重复；需要按层伤害用 DamageEffect.ScaleByBuffStacks。Independent 每次新建实例；Refresh 刷时长不叠层；AddStack 封顶且刷新；Reject 不刷新。事件明确：初次 Apply，实际增层 Stack，刷新 Refresh 记录；到上限再次应用不假报 Stack。

## 执行与清理

- 实例保存完整 EntityId 来源、施加时攻击和 Power，触发时重新解析，绝不长期保存 Actor。来源失效时按无来源，快照攻击/倍率仍可用于剩余周期，不能把同索引新实体认成旧来源。
- 本实例逻辑年龄用 double，只有 Buff 阶段执行才推进，Hitstop 冻结。出生帧不推进；后续 interval 整数时点触发，寿命为 [0,duration)，等于终点不触发周期；跨步先补齐寿命内周期再到期。刷新保留周期相位，并从当前年龄延长寿命。
- Apply/Stack/Period/Expire/Removed 的 Source=解析后施加者、Target=Buff持有者，Point=持有者位置，攻击/Power=实例快照。Hurt 的 Source=本次攻击者、Target=持有者、攻击=攻击者当时值；Cast 的 Source/Target=持有者。Hurt 不响应 Buff 诞生同帧的伤害，沿用现有行为。
- Remove 先从活动列表撤下并释放 Modifier/Tag，再投递回调。自然到期先 Expire 再 Removed；驱散/互斥/死亡/手动移除只 Removed。卸载只静默清理并记原因，不在卸载回调中生成新战斗内容。Clear 期间拒绝重入新增。
- TagComp 增加可释放租约，Buff 持有每个授予 Tag 的独立租约。普通 Remove 只能移除普通写入；释放某 Buff 不能减少其他 Buff/状态的资格。
- 所有遍历使用本次实例快照并检查仍存活，回调不能覆盖共享 scratch。相同实例的同类回调不重入，最多128实例/Actor、回调嵌套最多16层；拒绝值有实际事件记录，不静默无限递归。
- 移除旧 Pack(source) 哈希身份路径，按完整 EntityId 驱散；修改独立样例和现有调用点，不增加兼容 shim。伤害仍只由 World.Deliver/EffectPipeline/DamageEffect 结算。

## 引用与发布

依赖索引扩展为具名技能/Buff两个类型，应用与按Buff驱散都包含定义依赖；Buff图可生成召唤，进而依赖蓝图攻击技能。编译前建立定义身份，拒绝跨定义递归环及超过8层引用路径；发布裁剪完整技能/Buff/蓝图闭包，两槽相同定义不同版本拒绝。这里拒绝循环派生，不擅自把延迟解释成无上限递归。

## 验收

JSON/图往返、所有策略/上限/互斥、属性一次挂载、Tag租约、完整来源代数、快照/倍率、所有回调上下文与顺序、微间隔/到期/Hitstop、回调移除/重入、按来源/Tag/组驱散、两槽冲突/闭包及网页编辑/发布→Unity实际对照。运行 Core build/regress、宿主自检、前端构建、Unity EditMode/PlayMode与Error日志。
