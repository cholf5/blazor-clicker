# ADR 0002：M1–M4 实现决策

- **状态**：已采纳
- **日期**：2026-07-04
- **前置**：[ADR 0001](0001-technology-stack.md)

---

## 背景

ADR 0001 定下 Blazor WASM + `Game.Core` 纯逻辑类库的方向。这一份 ADR 记录
M1（建筑）→ M4（存档）+ 金饼干这一段实现中做出的具体决策，以及主动**没有**
做的取舍。

范围：一次会话内构建出的可玩版本，`Game.Core` ~600 行 C#，`Game.Web` 8 个
Blazor 组件 + 1 个 Home shell，33 个 xUnit 单元测试全绿。

---

## 采纳的决策

### 1. 数据驱动的静态目录

**决策**：所有建筑、升级、成就的定义都放在 `Game.Core/Data/*.cs` 的静态列表里，
`record` 类型，运行时不可变。

```csharp
public sealed record BuildingDefinition(BuildingId Id, string Name, string Icon,
                                        double BaseCost, double BaseCps, string FlavorText);
public static readonly IReadOnlyList<BuildingDefinition> All = [ ... ];
```

**理由**：
- 加内容 = 追加一行 `record`，不用改逻辑
- 静态数据 = 零并发问题、零加载延迟、编译期字面量
- `sealed record` + 主构造器让每一行数据都自我文档化
- `IReadOnlyList` + 私有 `Dictionary` 索引，两种访问方式各取所需

**没选的方案**：从 JSON 文件加载。JSON 会引入 IO + 反序列化 + schema 校验的三层
复杂度，对 12 个建筑 / 55 个升级的规模是过度设计。真到需要外部编辑时再切换。

### 2. `IsUnlocked` 用 `Func<GameState, bool>` 而非 DSL

**决策**：升级和成就的解锁条件是嵌入代码的 lambda：

```csharp
IsUnlocked: state => state.BuildingCounts.TryGetValue(BuildingId.Cursor, out var c) && c >= 25
```

**理由**：
- 直接享受 C# 类型系统与 IDE 智能提示
- 加复杂条件（"拥有 X 且已购买 Y"）不用扩展 DSL
- Cookie Clicker 原版里的解锁逻辑也是硬编码的，本质上属于**规则**而不是**数据**

**代价**：不能从 JSON 加载解锁条件。可以接受。

### 3. 效果系统：枚举 + 参数，而非多态子类

**决策**：升级效果统一走 `(EffectKind, EffectValue, TargetBuilding?)` 三元组，
`GameState` 里根据 `EffectKind` 走 switch/循环：

```csharp
public enum UpgradeEffectKind {
    BuildingMultiplier, ClickMultiplier,
    GlobalCpsMultiplier, CursorPerNonCursorBuilding
}
```

**理由**：
- JSON 序列化直接对上，不用配 polymorphic converter
- 增加效果类型 = 添枚举 + 加一段计算代码，位置集中在 CPS 汇总函数内
- 现阶段效果种类少（4 种），子类多态的开销大于收益

**代价**：效果计算逻辑集中在 `GameState.GetBuildingUnitCps` / `CurrentCpsRaw` /
`ClickPower`。如果效果类型爆炸，再重构成子类多态。

### 4. `GameState` 是单一聚合根

**决策**：不拆 Building / Upgrade / Achievement 三个 service，全部作为方法挂在
`GameState` 上：`Click / BuyBuilding / BuyBuildingBulk / BuyUpgrade / Tick /
ClickGoldenCookie`。

**理由**：
- Cookie Clicker 的状态是**强耦合**的：CPS 依赖建筑数 + 升级 + buff；成就依赖
  几乎所有字段；金饼干效果直接改 cookies——拆开会产生大量参数传递
- 单一聚合根 = 存档 = 一个 DTO
- 测试时构造成本极低（`new GameState { Cookies = X }`）

**代价**：`GameState.cs` ~300 行。仍在可读范围，但要盯着别继续膨胀，
未来若加转生系统再考虑抽 `EconomyModule` / `PrestigeModule`。

### 5. 存档：版本化 JSON + 可选 Base64 包裹

**决策**：
- `SaveData` 是一个独立 DTO，`SaveSystem` 是无状态静态类
- 版本号字段 `Version`，未来的 `Migrate()` 逐版本升级
- 导出串默认 Base64（避免 JSON 特殊字符在聊天窗口被吞），导入自动识别 raw JSON

**理由**：
- 分离 DTO 与聚合根，任何时候都能修改 `GameState` 内部实现而不破坏存档兼容
- 版本号是"未来自我"必备的保险
- Base64 兼容原版 Cookie Clicker 的存档格式惯例

### 6. Blazor 三栏 shell + `Services/SaveCoordinator` + `Services/GameLoop`

**决策**：
- `SaveCoordinator` 拥有当前 `GameState` 实例（**可替换**）
- `GameLoop` 通过 `SaveCoordinator` 每 tick 读取当前状态，而不是持引用
- Home 页监听 `SaveCoordinator.OnStateReplaced` 事件，让 import/wipe 自动生效

**理由**：
- 存档导入会**替换**整个 `GameState` 实例，如果任何组件持有旧引用就会指向孤儿数据
- 单一集中点管理"当前 state"，其他人只通过 coordinator 访问

**代价**：多一层间接引用。可接受。

### 7. 30 FPS tick + delta 时间

**决策**：`System.Timers.Timer` 每 ~33ms 触发一次；`GameLoop` 用真实经过时间
（`DateTime.UtcNow` 差值）作为 delta 传给 `Tick(deltaSeconds)`；最大 delta 截断到 5 秒。

**理由**：
- 后台标签页可能长时间不 tick；截断避免"离线 8 小时 → 一瞬间产出爆炸"
- 用真实 delta 而不是固定 1/30 = 帧率漂移不改变产出
- 30fps 对 Cookie Clicker 视觉够用，UI 重渲染压力小

**代价**：不做严格离线奖励（原版有）。可以后续把"截断 5 秒"改成"离线 X 时长 × Y%"。

### 8. 测试策略：只覆盖 `Game.Core`

**决策**：所有 xUnit 都写在 `Game.Core.Tests` 里，不做 Blazor 组件测试（bUnit 等）。

**理由**：
- 组件是**渲染层**，逻辑已经在 domain 层覆盖了
- bUnit 学习成本 + 维护成本大于收益
- 33 个用例已覆盖：公式、购买流水、升级效果应用、成就解锁触发/去重、存档往返、
  金饼干生成/点击/过期/buff 过期

**代价**：UI 层回归靠人工 smoke。真出问题时优先补 domain 层测试而不是 UI 测试。

### 9. 数字用 `double`，暂不引入 BigNumber

**决策**：所有数字（cookies、cps、cost）都是 `double`。

**理由**：
- 玩到 1e15 都还是精确整数，前中期无损
- 后期精度损失只影响显示，不影响相对增长
- 引入 BigDecimal / 自研 BigNumber 会污染整个 domain 层

**代价**：极后期数字精度和显示会失真。真的玩到那一步再说。

---

## 主动没做的事（记录在案）

按重要性排序，这些不是遗漏，是**明确决定推迟**：

1. **点击时的 CPS 增益**：`ClickPower()` 里已经加了 `+CurrentCps() * 0.01`，
   跟原版一致，不过更复杂的 lucky/frenzy 交互没做
2. **神殿 / 巫师塔 mini-game / 转生系统**：原版核心特色，工程量极大，另开里程碑
3. **音效**：Blazor 里做起来简单（`<audio>`），但需要声音素材
4. **服务器排行榜 / 云存档**：跟"零运维"目标冲突
5. **国际化**：全部英文文案硬编码，中文界面留待未来
6. **完整移动端体验**：三栏在窄屏折叠成一栏够用，但触摸手感没优化
7. **动画性能**：CSS keyframe 动画在低端机可能卡，需要时再降级
8. **BigNumber**：见上

---

## 已知的限制 / 债务

- `Data/Upgrades.cs` 里 Cursor 系升级的 EffectValue 语义有点绕（累加式），
  加注释了但 API 不够自解释，未来重构效果系统时一起处理
- `AchievementToasts.Pump()` 由 Home 主动调用是有耦合的，可以改成事件订阅
- `GameLoop` 用 `System.Timers.Timer` 而不是 `requestAnimationFrame`，
  失焦时仍会跑；如果电池成为问题，切成 JS interop 拿 rAF
- 没有 error boundary / 全局异常提示，Blazor 默认那条黄条够用了

---

## 参考

- 项目 README：`../../README.md`
- 上一条 ADR：`0001-technology-stack.md`
- 原版官方浏览器版：<https://orteil.dashnet.org/cookieclicker/>
