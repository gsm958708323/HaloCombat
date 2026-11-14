# 2026-11-15 Timeline控制标签与动作速度验收

分支`codex/web-skill-editor-json`，上一重要节点`acb722a`。本批按ADR-0021实现通用ControlTags/ActionSpeed，不声称历史十三技能、原Animator状态和资源已全部迁移。

## 实现与数据边界

显式Timeline新增controlTags与scaleWithActionSpeed。控制标签只允许1011..1016六种限制，重复、null、未知及Dead等状态标签在编译时拒绝。开始播放取得导演自己的TagLease，结束、替换、打断、取消和卸载释放；外部Buff同名租约不受影响。未知JSON字段继续拒绝，没有SO/代码回退。

Actor/PreviewActor/SummonBlueprint增加默认1的actionSpeed，AttrId追加ActionSpeed=9，不改变原槽位；构造/默认初始化/回收都恢复1。Buff修正新增actionSpeed，动作速度每步读取最终属性并取max(.1,值)；只有明确开启缩放的Timeline使用scaled dt。冷却、能量恢复、Buff时长、角色钟、已存在弹体/AoE和木桶周期不随动作速度加速。自身独立顿帧继续冻结动作时钟。

Motor保留现有Activity策略，叠加六种独立限制：

| Tag | 作用 |
| --- | --- |
| 1011 BlockMove | 禁止普通走动/主动跳跃，允许Timeline自身增量位移 |
| 1012 BlockRotate | 禁止持续转向/后续吸附；动作开始前已有外部限制也受尊重 |
| 1013 BlockSkill | 禁止新启动技能，当前Timeline继续；失败不扣资源 |
| 1014 BlockSkillMotion | 丢弃本步技能位移，冲刺不扫掠未走过的路径，不在解除后补积压 |
| 1015 BlockHitMotion | 丢弃本步受击位移，不隐式禁止普通走动 |
| 1016 BlockGravity | 暂停重力积分，保留空中竖直速度，解除后继续 |

显式Teleport仍使用原施放/落点契约，不把1014当作全位移禁止。控制不替代活动状态和伤害守卫，不新增技能专用状态机。

初次吸附读取SkillAim显式方向，在取得新Timeline控制前执行；Facing模式可有Yaw而无Point，默认教学调用未提供Yaw时仍使用已有输入朝向。修复直接施放中移动沿旧身体方向、投射物沿鼠标方向的不一致。非有限Yaw在付费/取得控制前拒绝。

网页显式时间轴可勾选六种控制/速度缩放；试验角色、召唤蓝图与Buff属性修正可编辑动作速度。实例显示真实动作时间/时长、有效倍率和自有控制。Unity PoseSample传递动作时间，程序化姿态按真实Timeline相位采样，不伪造Animator资源。示例42个；正式JSON新增arena-controlled-shot（2015/TL3015），其控制为1011/1012/1013，.6秒动作、前.4秒位移2米、.2秒发射、开启速度缩放；不是覆盖历史某个旧技能身份。

默认空控制/false缩放/速度1不写入协议文本，避免无语义变更使现有formatVersion1发布修订失效。独立读回旧用户F6/F7成功，不需要重新发布；实际编辑到非默认值时明确进入修订计算。

## 最终执行证据

| 检查 | 结果 |
| --- | --- |
| Core build/regress | 0错误；ALL S1+S2 REGRESSION PASSED；既有net6.0提示保留 |
| CLI正式JSON | 12技能/10角色，g1 3伤害事件/22生效伤害/0.59999996位移，JSON RUNTIME PASSED |
| 宿主自检 | 二倍Timeline、5步触发.2动作秒Payload、2米位移和控制清理通过，既有自检通过 |
| 前端转换/构建 | 30/30；TypeScript/Vite通过，index-qHPNY5-x.js / index-DfNjODZv.css |
| Unity EditMode | job `21157690fed6440fb517a8c97c3a5d1f`，424/424 Passed，0失败/跳过 |
| Unity PlayMode | job `1cac8f6ec2c145159254b36f0de24546`，56/56 Passed，0失败/跳过 |
| 跨宿主实际发布包 | 八份完整快照，4452字段和值，0差异；结构/文本/顺序严格，浮点绝对1e-5/相对1e-6 |
| 正式Arena实际文件 | 第10步x2/z0、动作.4秒/倍率2、控制1011/12/13；第15步结束、控制空、普通走动恢复 |
| 最终Unity Error | 0 |

新增33项EditMode覆盖六限制独立行为、初次/外部/后续朝向、无真实位移时冲刺不能造成假扫掠、被阻增量不补发、Teleport边界、外部控制Lease隔离、Stop/替换/失败、.5/1/2/0速度/动态Buff/关闭缩放、原定总位移、冷却/恢复/Buff/既有弹体计时、自身顿帧、坏控制/速度/非有限方向与默认字段省略。PlayMode新增3项覆盖实际磁盘发布、打断后恢复移动、正式Actor动作速度/实际视图/重开，以及坏控制保留当前有效世界。

## 浏览器实际编辑

从42模板临时工程选择“受控位移射击”，把Caster动作速度1→2。关闭缩放后施放5步：角色钟.10秒、动作.10秒/1×、x=-5.5000005；开启缩放后同样5步：角色钟仍.10秒、动作.20秒/2×、x=-5.000001，并实际生成1枚弹体。没有改变预览播放倍速来替代技能动作速度。

实际勾选“禁止技能增量位移”后重建并施放，5步角色x=-6不动，动作仍.20秒/2×、四个控制1011/12/13/14；该标签只阻增量位移，发射仍发生。解除该项后施放15步自然结束，角色x=-4.000002，完成原定2米，实例显示无当前技能/无控制、倍率恢复1。将正常三控制/二倍Actor配置发布F6，普通射击发布F7，保存后复制真实发布文件作证据。

截图timeline-controls-browser/scaled-controls.png同时显示动作时钟、Actor速度和六个控制编辑项。验收后恢复原23技能工程与原F6/F7，文件SHA256匹配备份，浏览器显式重新载入磁盘/预览就绪，临时1920×1080尺寸已撤销。正式角色默认仍为1，没有把网页试验速度2写回正式玩家配置。

## 同发布文件跨宿主对照

使用实际网页F6/F7，Actor速度2，F6控制1011/1012/1013。两条独立会话：自然播放后接F7；以及5步打断后恢复普通移动。宿主与Unity SkillLab共八快照比较4452位置一致：

| 场景/标签 | 固定步 | X | Z | 动作时间/倍率 | 控制 | 能量 |
| --- | ---: | ---: | ---: | --- | --- | ---: |
| 自然5 | 5 | -5 | 0 | .20/2 | 1011/12/13 | 91 |
| 自然10 | 10 | -4 | 0 | .40/2 | 1011/12/13 | 92 |
| 自然15 | 15 | -4 | .10 | 已结束/1 | 无 | 93 |
| 自然后普通射击35 | 35 | -4 | .70 | 已结束/1 | 无 | 87 |
| 打断前5 | 5 | -5 | 0 | .20/2 | 1011/12/13 | 91 |
| 立即打断 | 5 | -5 | 0 | 已取消/1 | 无 | 91 |
| 打断25 | 25 | -5 | 0 | 空闲/1 | 无 | 95 |
| 恢复走动26 | 26 | -5 | .10 | 空闲/1 | 无 | 95.2 |

自然第15步沿既有组件→Motor顺序在动作结束时释放控制，本步Motor随即恢复普通走动；因此Z已前进一个.10米步长。这是当前世界阶段约定，不把该结果称为与旧分支双相位Motor完全相同。原定Clip增量始终为X方向2米，普通走动是独立恢复后的输入。

## 实际正式Arena

MCP创建独立临时正式内容，玩家actionSpeed2，F6=arena-controlled-shot；提交MoveZ1并真实施放。第10步：x2/z0、TimelineTime=.399999976、ActionRate2、自有控制1011/12/13、Energy≈92，UnityActorView采样同一位置。第15步：动作自然结束、控制空、普通走动z=.06（Arena速度3对应.02秒步）。没有更改源project.json的玩家速度。临时内容和发布文件已归档到忽略证据目录arena-project，StreamingAssets不保留试验副本，Arena场景验证通过。

## 后续范围

N03中的ControlTags/ActionSpeed缩放、真实时间轴/位移控制已接通；AnimatorState、旧Controller/角色/动画/Cue资源与全部旧十三技能仍待N02/N03/N08验收。参数化技能的自动引导/蓄力时序未添加该开关，本批缩放明确属于显式Timeline，不擅自改变其持续扣能契约。完整运行内容包导出、事件作者定位/输入复现和视频最后视觉/交互对照继续进行，整体目标保持进行中。

本机证据在Tools/SkillLab/.local/verification：timeline-controls-build/host/regress/cli/authoring/web日志、unity-edit-timeline-controls.json、unity-play-timeline-controls.json，以及timeline-controls-browser内真实发布/host与unity快照/comparison.json/arena-result.json/scaled-controls.png。提交保留源码、JSON、测试、ADR和本文，生成Unity工程文件和用户.gitignore改动不纳入。
