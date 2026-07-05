# ADR 0005：移植牛奶（Milk）系统作为纯增长的后期乘数轴

- **状态**：已采纳
- **日期**：2026-07-05
- **前置**：[ADR 0004](0004-scope-boundary.md)

---

## 背景

[ADR 0004](0004-scope-boundary.md) 把项目钉死在「单一清晰的进度轴」上，
主动拒绝了 Garden / Stock Market / Pantheon / Grimoire / Wrinklers 等
**主动管理型**后期系统。那份文档同时留了一句话：

> 若要继续扩充……正确方向是**在纯增长轴上加**，而不是靠新系统凑数。

本 ADR 是对这句话的第一次正面应用，也回答一个具体问题：**原版的牛奶
（Milk）系统要不要移植？**

移植前的现状是——Kitten（猫咪）系升级存在，但被简化成了**写死的固定
全局倍率**：

| 升级 | 原实现 |
| --- | --- |
| Feline apprentices | 全局 CPS ×1.10 |
| Feline workforce | 全局 CPS ×1.25 |
| Feline engineers | 全局 CPS ×1.50 |
| Feline professors | 全局 CPS ×1.75 |

这四个升级买完即到顶，与成就数量无关。而在原版里，它们的效果是「每单位
牛奶百分比」，牛奶随成就解锁单调上涨，所以这条乘数轴会**持续变强**。简化
后的版本掐断了这条轴，导致后期一旦买完固定倍率，数值成长就少了一根本该
一直发力的复利来源。

结论是——**移植**。理由与设计见下。

---

## 采纳的决策

### 1. 牛奶不违背 ADR 0004，与 Wrinklers 有本质区别

**决策**：牛奶落在 ADR 0004 §5 认可的「在纯增长轴上加」范围内，**不**属于
§2–§3 拒绝的主动管理型系统。

**理由**：

- 牛奶是**纯被动**的：成就解锁 → 牛奶等级上涨 → Kitten 升级把牛奶转成
  全局乘数。玩家全程**不需要任何操作**，没有微操、没有「没最优操作 = 亏」
  的心理负担。
- 与 Wrinklers 的关键区别在于——Wrinklers 引入的是「要不要主动触发一个
  负面事件换收益」的**风险决策**，会污染干净的增长轴；牛奶只是给已经存在
  的「成就里程碑」这条轴**再挂一个复利放大器**，方向完全一致，不引入任何
  权衡或岔路。
- 它不是「拯救纯增长玩具的新维度」，而是「让纯增长玩具后期不断档的底料」。

### 2. 牛奶是派生量，零状态、零存档迁移

**决策**：牛奶**不作为独立字段存储**，而是从成就数量现算：

```csharp
public double MilkFactor() =>
    UnlockedAchievements.Count * ProgressionConfig.MilkPerAchievement;
```

**理由**：

- 牛奶完全由 `UnlockedAchievements.Count` 决定，存它是冗余；现算与
  `CurrentCps()` / `ClickPower()` 等既有查询同构（UI 每帧 poll）。
- **因此 `SaveData.Version` 保持 3，无需迁移**。老存档加载后牛奶自动按其
  成就数派生，不多不少。这正好落在 CLAUDE.md「新计数器默认 0/false 的老
  存档无需 backfill」精神的延伸——这里连计数器都不用加。

### 3. 用新的 `KittenMilkMultiplier` EffectKind，让 catalog 消费牛奶

**决策**：新增一个 `UpgradeEffectKind.KittenMilkMultiplier`，由 CPS 数学
通用解释——有效倍率 = `1 + MilkFactor() * EffectValue`。四个 Kitten 升级
从固定 `GlobalCpsMultiplier` 改为这个 EffectKind，`EffectValue` 从「倍率」
（1.1）改为「牛奶效力系数」：

| 升级 | 效力 (`EffectValue`) | 满配前含义 |
| --- | --- | --- |
| Feline apprentices | 0.05 | 每 100% 牛奶 +5% CPS |
| Feline workforce | 0.10 | 每 100% 牛奶 +10% CPS |
| Feline engineers | 0.15 | 每 100% 牛奶 +15% CPS |
| Feline professors | 0.20 | 每 100% 牛奶 +20% CPS |

**理由**：

- 完全遵循「加内容 = 往 catalog 加定义」的既定约束（ADR 0002 §1）：
  `GameState` 的 `CurrentCpsRaw` 只多一个 `else if` 分支，四个升级的差异
  全部落在数据里，没有散落的条件判断。
- 效力递增（0.05→0.20）还原原版手感：越后期的 Kitten 升级把同样的牛奶
  放大得越狠。

### 4. 数值：每成就 +4% 牛奶，与原版一致

**决策**：`ProgressionConfig.MilkPerAchievement = 0.04`。

**理由**：

- 真正控制后期斜率的阀门是 **Kitten 升级的效力系数**（§3 那张表），牛奶
  本身只是一根随成就单调上涨的「进度条」。把牛奶固定在原版的 +4%/成就，
  能还原原版的数量级手感（约 334 个成就满配 ≈ +1336% 牛奶）。
- 所有可调数字都集中在 `ProgressionConfig`（牛奶斜率）和 `Upgrades.cs`
  （Kitten 效力），符合「经济常量集中放置」的约定，事后调平衡成本极低。
  若实测发现与 prestige / 糖块叠加过猛，改一个常量即可，无需动结构。

### 5. Tooltip 显示实时倍率

**决策**：升级 tooltip 的效果行对 Kitten 升级显示**现算**的有效倍率
（`1 + MilkFactor() * EffectValue`）+ 每 100% 牛奶的加成率，通过
`TooltipService` 的 builder 每次渲染重算。

**理由**：

- 遵守 tooltip 实时刷新契约（见 `TooltipService` 与 `Game.Web.Tests`
  守护）：牛奶随成就增长，tooltip 必须反映当前值而非悬停瞬间的快照，
  绝不退回缓存 fragment。

---

## 主动没做的事

- **牛奶等级的视觉化（进度条 / 底部牛奶动画）**：原版把牛奶画成大饼干
  下方流动的牛奶。本次只接经济效果，视觉化留待后续（纯展示，不影响数值）。
- **牛奶风味升级（多种口味牛奶）**：原版有一串纯外观的牛奶口味切换，属
  装饰，与增长无关，不做。
- **牛奶影响其它系统**：原版牛奶还与部分后期系统联动；那些系统本项目
  按 ADR 0004 不做，联动自然也不存在。

---

## 已知的限制 / 债务

- 牛奶目前**只**通过 Kitten 升级发挥作用；在买下第一个 Kitten 升级之前，
  牛奶等级对数值毫无影响（与原版一致）。这是设计而非缺陷。
- §1 的判断依赖「牛奶保持纯被动」这一前提。若未来有人想让牛奶反过来
  要求玩家操作（例如「挤奶」小游戏），那会落回 ADR 0004 §2 的「主动管理」
  禁区，应新开 ADR 显式讨论，而不是顺势扩展本文。

---

## 参考

- 上一条 ADR：`0004-scope-boundary.md`（范围边界，本文是其 §5 的首次应用）
- 相关代码：`Game.Core/Domain/GameState.cs`（`MilkFactor`、`CurrentCpsRaw`）、
  `Game.Core/Domain/ProgressionConfig.cs`、`Game.Core/Data/Upgrades.cs`
- 原版官方浏览器版：<https://orteil.dashnet.org/cookieclicker/>
