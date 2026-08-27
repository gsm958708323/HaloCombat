# HaloCombat 纯 C# 战斗核（第一期总装）

## 运行

dotnet run
dotnet run -- tag
dotnet run -- attr
dotnet run -- buff
dotnet run -- motor
dotnet run -- clip
dotnet run -- melee
dotnet run -- proj
dotnet run -- season

默认 `season` = 第一期验收：G1 近战+刀光 Cue+火球灼烧、G2 火地叠 3、受击停轴、Bake 清缓存、死亡清弹圈。

## 冻结

Actor / Comp / World / Time / EventBus / IntentQueue / Pool
Combo 边表 + Play(skill, timeline)
InputBuffer 单槽 0.2s

## 唯一结算

IEffect.Apply(ref EffectContext) + World.Deliver
位姿：Request* → Locomotion.Integrate
血量：AttributeSet.SetBase(Hp)

## Unity

运行时源码位于 `Assets/Scripts/Combat/Core`。每个总装 Demo 都有一个场景，位于
`Assets/Scenes/HaloCombat`；场景中的 `HaloCombatDemoRunner` 会在 Play Mode 启动时运行对应
Demo，并把结果写入 Unity Console。

也可在 Unity 菜单使用 `Tools/HaloCombat/Create Demo Scenes` 重建场景，或使用
`Tools/HaloCombat/Verify Demo Scenes` 在编辑器中验证所有场景配置和 Demo。
