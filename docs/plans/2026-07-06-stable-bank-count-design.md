# 稳定左上角饼干总数的显示宽度

- 日期：2026-07-06
- 状态：已验证设计，待实施

## 背景与问题

左上角饼干总数在 `StatsPanel.razor:17` 的 `.stats-bank-count` 渲染，调用
`NumberFormat.Format(State.Cookies)`，容器为 `text-align: center`
（`StatsPanel.razor.css:2`）。

两层原因叠加导致数字"左右横跳"：

1. **末位 0 被吞** —— `NumberFormat.Format` 的大数分支用格式串 `"0.###"`
   （`NumberFormat.cs:47`）。`#` 是"可选数字位"，故 `1.230 million` 被压成
   `1.23 million`，字符数骤减。
2. **居中放大了它** —— 宽度一变，左右边同时移动，视觉上即为横向抖动。

`< 1,000,000` 时走整数（`N0`）分支，不带小数、不抖；只有进入 million 以上、
每帧末位小数刷新时才闪。

## 范围

**仅稳住左上角总数**（`.stats-bank-count`）。其他显示数字的位置（建筑成本、
升级价格、CPS、已烤总数等）保持原版"省略末尾 0"风格不变。"已烤"因单位已进入
更高量级、刷新频率低，跳动可接受，暂不处理。未来若发现其他位置闪动再单独处理。

## 设计

### 一、`NumberFormat` 新增稳定格式方法

在 `Game.Core/NumberFormat.cs` 新增独立方法 `FormatStable`，**不改动现有
`Format`**，避免影响全站 15+ 处调用：

```csharp
/// <summary>
/// Like <see cref="Format"/>, but keeps a fixed 3-decimal width on large
/// numbers (e.g. "1.230 million" instead of "1.23 million"). Used only for
/// the top-of-column cookie bank, where the value refreshes every frame and
/// a shrinking width in a centred box reads as horizontal jitter.
/// </summary>
public static string FormatStable(double value)
```

实现与 `Format` 几乎一致，唯一区别：大数分支格式串由 `"0.###"` 改为 `"0.000"`。
小数（`< 1,000,000`）分支仍走 `"N0"` 整数显示——本就不带小数、不会因末位 0
抖动，保持不变。

结果：`1.230 million`、`12.300 billion` 的字符结构恒为 `x.xxx <suffix>`，逐帧
只变末位数字、总字符数不变，居中也不横跳。

### 二、CSS 等宽数字

给 `StatsPanel.razor.css` 的 `.stats-bank-count` 加一行：

```css
font-variant-numeric: tabular-nums;
```

让 `0`–`9` 每个数字字形等宽。方法一保证"字符数不变"，这一行进一步保证"字符
宽度也不变"，消除末位数字切换（`1.231`→`1.238`）时的亚像素抖动。两者配合，
million 级以上逐帧刷新宽度完全恒定。

### 三、调用点 + 测试

- **调用点**：`StatsPanel.razor:17` 把 `NumberFormat.Format(State.Cookies)`
  改为 `NumberFormat.FormatStable(State.Cookies)`。其余 `NumberFormat.Format`
  调用一律不动。
- **测试**（CLAUDE.md 要求新 domain 行为配 xUnit）：在
  `tests/Game.Core.Tests` 加 `NumberFormatTests`，断言：
  - `FormatStable(1_230_000)` == `"1.230 million"`（守护末位 0 不被吞）
  - `FormatStable(1_234_500)` == `"1.234 million"`
  - `FormatStable(999_999)` == `"999,999"`（小数分支不带小数）
  - `FormatStable(-1_230_000)` 负号正确
  - `Format(1_230_000)` == `"1.23 million"`（守护旧方法未被误改）

## 明确不做（YAGNI）

- 不消除整数部分位数进位造成的低频宽度变化（`9.999m→10.000m`、
  `999.999m→1.000 billion`）。这属数量级前进、频率极低。真要消除需固定整数
  位宽或右对齐，超出当前范围。
- 不改 `Format` 本身，不影响其他调用点。
