# SO 内容管线与运行时调试踩坑记录

> 记录一次「让 Unity 运行时完全由 ScriptableObject 驱动」的改造（Buff Arena 内容管线 + 冒烟测试 + 死代码清理）中真实踩到的坑。
> 每条都注明**症状 → 根因 → 怎么确认 → 解法**，并附上当时实际用过、可复用的诊断片段。
> 涉及文件：`Assets/Scripts/Combat/Config/**`、`Assets/Scripts/Combat/Core/BuffArenaGameplay.cs`、`Assets/Scripts/Combat/Unity/**`、`Assets/Editor/HaloCombat/BuffArenaDatabaseBuilder.cs`、`Assets/Tests/**`。

---

## 1. SO 序列化坑（最贵的一类）

### 1.1 一个 .cs 文件里的多个 ScriptableObject：只有「主类」能序列化

**症状**：技能能放（时间轴正常推进、弹药消耗、Cue 播放），但**不生成子弹 / 命中不扣血**；或者测试在 SetUp 就报 `BuffArenaSession did not start`。

**根因**：Unity 只会给**与文件名同名的那个类**铸 `MonoScript`。如果一个文件里塞了多个 `ScriptableObject` 子类（且文件名不等于其中任何一个），Unity 会把**文件里第一个声明的类**当主类，其余类：

- `ScriptableObject.CreateInstance(type)` 创建成功、字段也写进去了 → **内存里一切正常**；
- 落盘时写成 `m_Script: {fileID: 0}`（并留下 `m_EditorClassIdentifier: <assembly>:<ns>:<Class>` 双前缀）；
- **域重载后反序列化成 `null`**。

于是引用了这些对象的数组/字段（timeline payload 的 effect 槽、projectile 的 OnHit…）变成 null 洞，运行时表现为「技能放了但什么都没发生」，**而且一行报错都没有**。

**怎么确认**：直接问 Unity 有没有 MonoScript，不要靠猜。

```csharp
// execute_code：全工程审计（只读，安全）
foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
  if (!asm.GetName().Name.StartsWith("Combat.")) continue;
  foreach (var t in asm.GetTypes()) {
    if (t.IsAbstract || !typeof(ScriptableObject).IsAssignableFrom(t)) continue;
    var so = ScriptableObject.CreateInstance(t);
    var ms = MonoScript.FromScriptableObject(so);
    if (ms == null || ms.GetClass() != t) Debug.LogError("NO MONOSCRIPT: " + t.FullName);
    UnityEngine.Object.DestroyImmediate(so);
  }
}
```

也可以直接看 `.asset` YAML：出现 `m_Script: {fileID: 0}` + 非空 `m_EditorClassIdentifier` 就是它。

**解法（三层，缺一层会复发）**

1. **源码**：一个 `ScriptableObject` 类 = 一个同名 `.cs` 文件。本次把 `BuffArenaEffectAssets.cs` 里的 7 个类拆成 7 个文件。
2. **数据**：已经写坏的资产要**重建**（见 1.3），光改源码不够。
3. **守卫**：
   - builder 干跑阶段拒绝「解析到的资产类没有 MonoScript」，写盘前抛异常：
     ```csharp
     static bool HasMonoScript(Type assetType)
     {
         var probe = ScriptableObject.CreateInstance(assetType);
         var script = MonoScript.FromScriptableObject(probe);
         bool has = script != null && script.GetClass() == assetType;
         DestroyImmediate(probe);
         return has;
     }
     ```
   - EditMode 测试 `EffectAssetScriptTests` 全工程扫，谁没有就红（不依赖"这个类有没有被用到"）。

> 已写入 README 冻结约定：「一个 ScriptableObject 类 = 一个同名 .cs 文件」。

### 1.2 内存里对的、落盘丢引用：运行时实例必须挂成 sub-asset

**症状**：`proj4001.OnHit = [NULL, NULL, NULL]`，敌人被子弹穿过不扣血。

**根因**：builder 用反射把运行时 `IEffect` 实例转成 `EffectAsset` 实例。这些实例是 `CreateInstance` 出来的**游离对象**，没有 owner 就不可能被序列化；父资产里的引用保存后即丢失。

**解法**：转换完立刻挂到父资产上，`AddObjectToAsset`：

```csharp
static void Attach(Object sub, Object owner) {
    if (sub == null) return;
    if (!AssetDatabase.Contains(sub)) AssetDatabase.AddObjectToAsset(sub, owner);
}
```
注意 parent 是**每类资产各一处**：timeline 的 payload effect、projectile 的 OnHit/OnExpire、AoE 的 OnHit…漏掉任何一处，那一处就是 null 洞（本次就是先修了 timeline、漏了 projectile，才出现"命中不扣血"）。

### 1.3 已经写坏的 sub-asset 删不掉（null 孤儿）

**症状**：修好源码 + 重建后，`.asset` 里仍然留着旧的 `m_Script: {fileID: 0}` 对象，越积越多。

**根因**：`AssetDatabase.LoadAllAssetsAtPath` 对「脚本缺失」的对象返回 **`null`**；而清理函数必须 `if (sub == null) continue;` —— **你删不掉你看不见的东西**。它们会永远烂在文件里。

**解法**：检测到就**整文件重写**（`DeleteAsset` + 重新 `CreateAsset`）。前提是那份资产完全由工具生成：

```csharp
static bool HoldsUnloadableSubAssets(string path) {
    var all = AssetDatabase.LoadAllAssetsAtPath(path);
    if (all == null) return false;
    for (int i = 0; i < all.Length; i++) if (all[i] == null) return true;
    return false;
}
```
**例外**：带人工授权数据的资产（`Cues.asset` 里手绑的预制体）要显式排除重写路径，否则会冲掉手工绑定。

### 1.4 「验证通过」也会骗人：必须 reimport 后验 + 保存前验

两条顺序纪律：

- **保存前验**：`Verify()` 必须在 `AssetDatabase.SaveAssets()` **之前**跑；不一致就抛异常、一个字节都不写，旧资产保持完好。否则会出现「半写入」状态：资产被写坏、Arena 又拒绝启动，而且没有回滚。
- **保存后复查**：`SaveAssets` + `Refresh` 之后再 `VerifyPersisted()`（重新 import）比对一次。因为引用能否活过重载，只有重新导入才看得见。

**关键认知**：在编辑器会话里对**内存对象**做比对，得到 `differences = 0` 完全不说明问题 —— 我这次就被它骗过一轮。磁盘真相只在**域重载之后**出现（测试运行器每次都会强制域重载，所以测试比"手动 Play 一下"可信得多）。

---

## 2. SO 索引 / 字段拷贝坑

### 2.1 反射拷字段只拷 public → 私有配置丢失 → 静默无子弹

**症状**：按下技能 1，没有子弹，**没有任何报错**。

**根因链**：

```
CopyFields 只遍历 public 字段
→ SpawnProjectileEffect._specId（private）没被拷贝
→ 资产 SpecId = 0
→ 运行时 Projectiles.TryGet(0) 失败
→ RuntimeBodies 里直接 return（静默）
```

**解法**：

- `CopyFields` 用 `BindingFlags.Public | NonPublic | Instance` 遍历源字段，目标侧仍只认 public，并做 `_x → X` 的大小写不敏感映射；
- 同时**收紧验证**（`CompareValue` 也要比 NonPublic）—— 这次正是收紧验证后，才一次性暴露出 30 处差异。

### 2.2 效果资产类名不规则 → 三级解析 + alias 表

**症状**：一次错误重构让 `DamageEffect` 去拼 `DamageEffectAssetAsset`，结果 **24 个效果 0 个解析成功**。

**解法**：`ResolveAssetType` 按「精确名 → 去掉 `Effect` 后缀 → 别名表」三级解析，并且**干跑预检**（`RequireAllEffectsResolvable`）——有任何一个效果类解析不到就中止，绝不写出「残缺但看起来正常」的资产。

```csharp
var exact = FindType("Combat.Config." + name + "Asset");            // DamageEffect → DamageEffectAsset
if (exact != null) return exact;
var stripped = FindType("Combat.Config." + name.Substring(0, name.Length - 6) + "Asset"); // PlayCueEffect → PlayCueAsset
if (stripped != null) return stripped;
if (EffectAssetAliases.TryGetValue(name, out var alias)) return FindType("Combat.Config." + alias);
```

### 2.3 私有字段命名约定：`_<AssetFieldName>`

效果类常常把配置放在私有字段里（ctor 注入）。反射拷贝要求**字段名能和资产的 public 字段对上**，所以约定：

```csharp
// 运行时（Core）
class PlayBuffArenaCueEffect { string _anchorKey; string _instanceKey; bool _targetIsVictim; bool _atCuePoint; ... }
// 资产（Config）
class PlayBuffArenaCueAsset  { public string AnchorKey; public string InstanceKey; public bool TargetIsVictim; public bool AtCuePoint; ... }
```
违反约定 = 静默少拷一个字段。

### 2.4 索引 / 长度对齐：`RequireSet` 那道闸门

`BuffArenaDatabaseAsset` 里三份「源游戏 id 名单」（`SkillIds` / `ProjectileIds` / `AoeIds`）**运行时不参与任何查找**，它们是校验数据：

- `BuffArenaGameplay.ValidateSource` 用 `RequireSet(name, actual, expected...)` 断言「资产 id 集合 == 代码表硬编码的名字集合」（**数量必须完全相等**，名字不查顺序）；
- `BuffArenaDatabaseAsset.HasCompleteContent()` 用它们的长度当平行数组的尺寸闸门；
- `OnValidate()` 在 Inspector 里对「projectile/AoE 数量少于 id 数量」报错。

**注意**：`Verify()` 比对的是**烘焙后的运行时数据**，它**看不到**这三份 id 名单 —— 所以这是 `Verify()` 覆盖不到的唯一一格。删掉它们 = 少一道防护，不建议为了"Inspector 短一点"就删。

---

## 3. 运行时调试坑

### 3.1 编辑器失焦会节流主循环

无人值守跑 Play 时必须：

```csharp
Application.runInBackground = true;
```
否则编辑器窗口一失焦，主循环被节流，测试/演示会**在第一帧就停住**，看起来像"逻辑没跑"。这条已写进 PlayMode 用例的 `[UnitySetUp]`。

### 3.2 「内存态正常」是陷阱

见 1.4。**排查顺序**永远是：先确认磁盘上的 YAML / 重新导入后的对象，再看运行时行为。手动进 Play 观察到的正常很可能只是内存里的残留。

### 3.3 编译失败时 `execute_code` 跑的是旧程序集

症状：编译明明报错，但 `execute_code` 里调 builder 仍然返回 `REBUILD OK`。

**原因**：编译失败 → 新程序集没加载 → 执行的是上一次成功编译的程序集。

**纪律**：`execute_code` 的任何结论，必须以「`read_console` 里 0 error」为前提。诊断脚本改完先看错误，再信输出。

### 3.4 在 Play 里复制测试路径做二分（本次最好用的手法）

测试失败时，不要在测试里反复加断言猜。**在 Play 模式里用 `execute_code` 把测试的调用序列**（`ApplyInput` → `PumpLogic` × N）**原样跑一遍，并把中间状态打出来**：

```csharp
var f = new Combat.Unity.Game.BuffArenaInputFrame();
f.Fire1Held = true;
s.ApplyInput(ref f);                                  // CodeDom 需要 ref（in 参数）
sb.AppendLine("afterApply hasBuffered=" + ib.HasBuffered);
for (int i = 0; i < 70; i++) {
  s.PumpLogic(0.02f);
  // 每 tick 打 time / buffered / director.IsPlaying / 弹体数 / 弹药
}
```
这次靠它一枪就定位了「测试的 struct 副本 bug」：同样序列在 Play 里第一个 tick 就 `proj=1 ammo=59`，而测试里什么都不发生 → 问题在测试侧，不在生产代码。

**CodeDom（`execute_code`）是 C# 6**：不能用 `out var`、局部函数、模式匹配 `is`；`in` 参数要用 `ref` 调。

### 3.5 Play 里被鼠标驱动的东西：朝向来自射线

`BuffArenaInputSource.Sample` 用 `Mouse.current.position` 投到地面算 `AimYaw`，`BuffArenaBootstrap.Update` 每帧 `ApplyInput`。所以**玩家的实际朝向取决于光标位置**：

- 手工 Play 时"看起来对"，只是光标恰好在那里；
- 测试里"把敌人放在正前方"会随机失败 —— 因为放置用的 yaw 和开火那一刻的 yaw 可能不是一个。

**解法**（测试侧，不改生产行为）：把开火帧的瞄准钉死，并保证目标可见：

```csharp
Press(f => { f.Fire1Held = true; f.AimValid = true; f.AimYaw = _aimYaw; return f; });
```
外加两点：按 30° 扫一圈找地图接受的方向（玩家出生点是随机的，正前方可能是墙）、把目标 AI 冻住（`BehaviorTreeComp.SetEnabled(false)`）以免它自己走开。

### 3.6 测试框架的副作用与竞态

- **会改项目设置**：每次 `run_tests {PlayMode}` 跑完，`ProjectSettings/EditorSettings.asset` 的 `m_EnterPlayModeOptions` 会停在 `1`（关闭域重载），框架不还原。**跑完测试立刻提交会把这个设置一起提上去** —— 提交前 `git status` 看一眼，需要时 `git checkout --` 还原。
- **重载期间调 `run_tests` 会得到空 job**：返回 `status=failed` 但**没有 summary、没有失败列表**。这不是测试失败，等状态回到 `idle` 重试即可（本次出现 2 次，重试都是 7/7）。
- **测试运行器会强制域重载**：这一点是好事 —— 它让"只在磁盘上坏"的问题必然暴露。所以**内容类改动必须以测试为准，而不是以手动 Play 为准**。

---

## 4. 测试自身的坑

### 4.1 `struct` + `Action<T>` = 改的是副本

```csharp
// 错的：Action<BuffArenaInputFrame> 里的 lambda 改的是副本
void Press(Action<BuffArenaInputFrame> configure) {
    var frame = new BuffArenaInputFrame();
    configure(frame);          // ← frame 依然全 false
    _session.ApplyInput(frame);
}

// 对的：让 lambda 把 frame 交回来
void Press(Func<BuffArenaInputFrame, BuffArenaInputFrame> configure) {
    _session.ApplyInput(configure(new BuffArenaInputFrame()));
    for (int i = 0; i < TicksPerSkill; i++) _session.PumpLogic(Step);
}
// 调用：Press(f => { f.Fire1Held = true; return f; });
```
这个 bug 的表现是「所有按键测试都失败」，非常容易被误判成生产代码坏了。

### 4.2 计数断言要取峰值

`Press` 里 pump 70 tick（1.4s）之后再数弹体，可能子弹早就命中/过期了。改成**在 pump 过程中取峰值**，否则断言会在"确实生成了但已经消失"时误报。

### 4.3 冒烟测试必须跑真实会话

只断言 `BakeContent() != null` 不够 —— 要加载真实场景、拿 `BuffArenaSession`、真的按键、真的看敌人掉血。本次的 null 洞就是被这种端到端断言抓到的。

---

## 5. 死代码清理的坑

### 5.1 「没有引用」不等于「死代码」

静态扫「类名在别处出现次数 == 0」会得到大量**假阳性**：

- effect 资产类由 builder **按名反射**解析，类名根本不出现在代码里；
- 有些对象只以 **sub-asset** 存在，代码里没有任何引用。

本次 32 个候选里只有一小部分是真死。

### 5.2 判据要双条件：代码引用 + GUID 实例

对每个候选类，同时查：

1. 类名在其他 `.cs` 里是否出现（`BindingFlags` 那套反射要单独评估）；
2. 该类 `.cs.meta` 的 **GUID** 是否在其他 `.asset / .unity / .prefab` 里出现。

两条都为 0，且它的引用者本身也在待删集合里（闭包），才可以判死。本次据此确认 `CombatDatabaseAsset` 全工程零实例、唯一调用者 `SoCombatContent` 从未被构造。

### 5.3 删除顺序：同文件残留引用会先编译失败

如果被删的类型在**另一个文件里**还有引用（例如 `DefinitionAssets.cs` 里的 `SoCombatContent` 引用了待删的 `CombatDatabaseAsset`），删完第一件事就是编译失败 —— 这是**预期内**的中间态，不是删错了。

正确节奏：**批量删除 → 立刻裁剪残留文件 → 再编译**。如果先编译再判错，会误以为删多了。

（同理：逐个 `delete_script` 每次都会触发一次导入/编译，Console 里会堆积中间态报错；清空 Console 后强制重编译一次，才是可信的 0 error。）

---

## 6. 可复用工具盒

| 场景 | 手法 |
| --- | --- |
| SO 类能不能序列化 | §1.1 的 MonoScript 审计片段 |
| 时间轴/payload 里有没有 null 洞 | 遍历 `Timelines[i].Bake().Payloads`，打 `Effects[e] == null` 的槽位；再看原始资产的 `Effects[e]` 是 `NULL REF` 还是 `Bake() → null`（两者根因不同） |
| 哪个类是真死代码 | §5.2 双条件扫描（名字引用 + .meta GUID） |
| 内容资产能不能用 | 找一台能编译的 Unity，`BuffArenaDatabaseAsset.Bake()` 返回非 null（引用校验都过）；或直接进 Play / 跑 PlayMode 冒烟。生成器与 `Verify` 菜单已随代码表一起移除 |
| Unity 侧跑测试 | `run_tests` + `get_test_job`（`include_failed_tests`）；PlayMode 前确认 `Application.runInBackground` |
| CLI 回归（零弹窗） | `SetErrorMode(0x0001|0x0002|0x8000)` + `DOTNET_EnableDiagnostics=0` + `[Process]::Start`（**不要** `Start-Process -PassThru`）+ 超时 + 异步重定向读 |
| Play 内探测会话 | §3.4 骨架；`ref` 调 `in` 参数、每 tick 打状态 |

---

## 7. 改内容 / 改 SO 后的检查清单

1. `read_console` 0 error（编译先过，否则后面结论都不可信）。
2. 跑 MonoScript 审计（§1.1）—— 尤其是新增了 `ScriptableObject` 类之后。
3. 进一次 Play（或重载域）让资产重新烘焙 —— Arena 的内容值与空弹回退技能都只在启动时读一次。（玩家按键表写死在 `BuffArenaSession.ApplyInput`，改键不用动资产。）
4. 检查 `Bake()` 是否通过：引用校验（技能 → 时间轴、空弹回退 → 技能、必需 blueprint）失败时场景启动即抛异常并带上 `LastContentError`。
5. EditMode + PlayMode 测试全绿（测试会强制域重载，等价于"从磁盘重来一次"）。
6. CLI `regress`：退出码 0、`ALL S1+S2 REGRESSION PASSED`、stderr 为空。（Arena 没有 CLI 用例；`buffarena` 命令已移除。）
7. `git status` 复核改动面：确认没有把 `ProjectSettings/EditorSettings.asset` 之类的框架副作用一起带走。

---

## 8. 一句话总结

- **SO 的坑几乎都不报错**：坏引用、丢字段、索引错位，全都会静默降级成"技能放了但没效果"。
- **所以判据只能是端到端**：真实会话 + 真实按键 + 真实掉血；`Verify()` / 内存比对 / 一次成功的手动 Play 都不算。
- **只有域重载后的磁盘状态算数**：测试运行器强制重载，这让它成为最可靠的守门人。
- **每一类坑都要留一个守卫**：MonoScript 审计、干跑预检、保存前 + 重导入后双重验证、端到端冒烟测试。守卫比记住教训可靠。
