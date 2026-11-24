# 2026-11-15 弹药、缺弹换弹与追踪弹重施放验收

分支`codex/web-skill-editor-json`，上一重要节点`a5a263c`。本批按ADR-0017接入通用弹药组件、缺弹替代技能、追踪弹身份和传送操作；木桶链爆、历史13技能与完整旧Arena内容仍未完成。

## 迁移规则

旧历史源码与资产确认了以下行为：Ammo容量/当前值属于角色运行态；技能的AmmoCost支付失败时播放自己的FallbackSkill；换弹Timeline总长1.15秒，在1.10秒Payload补充60弹；传送弹再次施放读取仍存活的完整弹体位置，落点受阻时消费传送动作但保留弹体。原加速传送弹运动使用`2 * Speed * age / (age + 5)`曲线。

当前实现将这些规则拆成通用模块：`AmmoComp`、`TrackedProjectilesComp`、`SkillCastRouting`、`RefillAmmoEffect`和`TeleportTrackedProjectileEffect`。技能编译器接受ammoCost/fallbackSkill/recastProjectile/recastSkill/requiredProjectile/trackingKey/rampTime；引用闭包、递归、配对关系、容量/初值和小数弹药均在JSON编译前拒绝。失败不扣能量、弹药或冷却；成功CastId和弹体完整EntityId用于重施放与回收隔离。

网页示例由34增至38个技能，其中包括“弹药射击”“换弹 · 1.1秒补充”“传送弹 · 再次施放传送”“追踪弹传送操作”。施放节点有“弹药与重施放”折叠区；投射物有来源追踪键和渐进加速曲线；试验资源显示弹药上限/初始弹药。技能改名、删除替代、复制粘贴、工程合并和发布闭包均会同步这些引用。

正式JSON新增四个通用技能、Arena Fire1弹药主攻击与Fire4追踪弹绑定；Arena正式玩家容量为60。按住Fire1完成动作后连发，缺弹播放配置换弹轴，仍使用统一施放守卫。Fire4通过`ArenaTracked`进入“传送弹 · 再次施放传送”，成功传送使用实际弹体位置，阻挡时保留弹体并在HUD显示“Destination blocked”。历史F6/F7用户槽在验收开始前备份，网页临时发布验证后恢复；正式工程仍是JSON唯一来源。

## 验收证据

| 检查 | 结果 |
| --- | --- |
| C# Core build | `dotnet build Combat.csproj` 通过，0错误 |
| Core regress | ALL S1+S2 REGRESSION PASSED |
| 宿主自检 | 延迟换弹、真实扣弹、发布闭包、传送重放、正式Arena ramp曲线均通过 |
| 网页 authoring | 28/28，包含改名/删除/合并/粘贴引用处理 |
| 网页 build | TypeScript/Vite通过，`index-D774jzBM.js` |
| Unity EditMode | job `3d2b27f4d4a844bc92a0fc24d527a04c`，327/327 Passed |
| Unity PlayMode | job `8db95bb6ec7d4704921b889dcbf3a45c`，44/44 Passed |
| Unity Error | 0 |

EditMode覆盖换弹Payload边界、缺弹和能量联合支付、失败零扣费、忙状态、追踪弹实际位置、传送后回收、阻挡重试、寿命/手动回收、完整EntityId代际、发布依赖、循环/坏引用/容量校验和组件池生命周期。PlayMode覆盖真实Arena磁盘F6/F7、HUD弹药、Fire4传送、阻挡保留和重开事件隔离。

实现追踪回收时发现注册表DetachAll后，工厂ResetForPool还会再次Detach；旧OnDetach只清字段，新增关联清理暴露了双调。Comp.Detach现在每次Attachment只调用一次OnDetach，独立回归覆盖重新Attach后再次正确释放。原Arena保持技能测试构造的临时弹体数字身份与新4001/4004冲突，测试改为从当前目录分配独立身份，未删除保持/取消断言。

## 网页真实操作

在临时副本工程中将弹药上限设为2、初始弹药设为1、弹药消耗设为2。第一次施放进入缺弹换弹；逐帧到1.08秒时仍为1发，1.10秒事件记录`Refilled 1 -> 2/2`；下一次施放扣到0并生成1枚投射物，发布到F6。追踪弹施放26步后运行实例显示`追踪键 warp`、路径3.12米；再次施放后事件记录`弹体传送 caster warp / Teleported`，实体回收，角色从x=-6移动到x=-2.4760003。发布到F7后保存并恢复原23技能工程、原F6/F7。

证据截图：`Tools/SkillLab/.local/verification/ammo-browser/reload-desktop.png`、`tracked-published.png`、`unity-published.png`。这些文件保持在Git忽略证据目录，不进入提交。

## 宿主/Unity完整快照对照

临时发布包F6/F7在宿主与Unity SkillLab中运行同一固定输入，保存`f6-54`、`f6-55`、`f6-98`、`f7-26`、`f7-27`五份快照。逐字段比较共**2099个字段和值，0差异**；比较结果保存在`Tools/SkillLab/.local/verification/ammo-browser/comparison.json`。关键投影如下：

| 场景 | 步数 | 弹药 | 施放 | 投射物 | 角色X |
| --- | ---: | ---: | ---: | ---: | ---: |
| F6缺弹换弹 | 54 | 1 | 1 | 0 | -6 |
| F6换弹完成 | 55 | 2 | 1 | 0 | -6 |
| F6再射击 | 98 | 0 | 2 | 1 | -6 |
| F7弹体存在 | 26 | 1 | 1 | 1 | -6 |
| F7二次传送 | 27 | 1 | 2 | 0 | -2.476 |

正式Arena ramp曲线宿主检查在0.44秒年龄得到弹体x=0.6098128、速度0.92988926，保留历史渐进加速。正式Fire1按住90步/松开30步现在得到3次施放/3次命中/150实际伤害、57剩余弹药；前一Arena入口验收的G1主攻击220伤害属于当时旧绑定，不能作为当前主攻击结果。

## 未包含范围

本批没有宣称木桶伤害上限/自伤/死亡链爆、历史13技能全部节点和资源、角色随机成长、ControlTags/动作速度及原动画完整迁移。下一重要节点继续恢复木桶领域能力并与Arena障碍/表现对照，验收后单独提交。
