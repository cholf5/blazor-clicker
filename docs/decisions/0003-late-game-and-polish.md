# ADR 0003：M5 后期系统 + 手感增强

- **状态**：已采纳
- **日期**：2026-07-04
- **前置**：[ADR 0002](0002-implementation.md)

---

## 背景

ADR 0002 明确把音效、粒子、新闻栏、后期建筑、糖块、转生、离线收益列在
"主动没做" 的清单里。本 ADR 记录 M5 这一轮把它们全部补齐时的决策。

范围：`Game.Core` 从 ~600 行增长到 ~800 行；`Game.Web` 增加 5 个组件、
1 个 `AudioService` + 1 段 vanilla JS；xUnit 覆盖从 33 → 51 个用例；存档
schema 从 v1 升到 v2 并带一条无损迁移路径。

---

## 采纳的决策

### 1. 后期建筑：直接扩枚举，不重构目录

**决策**：`BuildingId` 枚举后面追加 6 个新条目（Antimatter condenser、
Prism、Chancemaker、Fractal engine、Javascript console、Idleverse），
`Buildings.All` 里对应加 6 行 `record`。

**理由**：
- ADR 0002 §1 已经确定"加内容 = 追加一行 record"，事实证明这个约束
  在 M5 完全成立：Tier 升级、成就（`own_X_1/50/100/150`）、UI 展示都
  是从 `Buildings.All` 生成的，加建筑不用改任何逻辑代码
- 12 → 18 个建筑不是数量级差异，不需要引入分页 / 分类
- 极后期基础成本已经用到 `double` 上限的一小段（1.2e22），仍在 `double`
  精确整数范围之外的一小段容差以内——ADR 0002 §9 的 BigNumber 决策依然
  可以推迟

**代价**：Legendary tier 升级对 Idleverse 达到 `~6e28`——展示会用科学计数法，
但游戏内不可能真的达到这个价钱，也就没有实际问题。

### 2. 糖块：单一 ready-boolean + 累计计数

**决策**：糖块系统是最小实现——

```csharp
public long SugarLumps { get; private set; }        // 累计已收
public bool SugarLumpReady { get; private set; }    // 是否有一颗成熟等采
public double SugarLumpNextAt { get; private set; } // 下一颗成熟的游戏时间
```

一次最多只有一颗未采糖块。默认成熟时间 30 分钟。糖块效果：每颗 +1% CPS，
永久，不消耗。

**理由**：
- 原版糖块是可消耗的、有稀有度、可以喂建筑升级——工程量至少一个礼拜
- "永久 +1%" 让糖块有存在感又不用做花式 UI（点掉就完了）
- 单个 `bool + double` 存档只多两个字段，migration 免费
- 后续要接原版的 "spend on building level" 时，`long SugarLumps` 已经是
  余额概念，再加一个 `BuildingLevels: Dict<BuildingId,int>` 就行

**代价**：糖块暂时不能消耗——没有花钱的地方就没有取舍。留待未来加建筑
等级 / 迷你成就时补完。

### 3. 转生（Ascend）：cbrt 公式 + 单一 `PrestigeLevel`

**决策**：`Ascend()` 是一个纯 domain 方法——

```csharp
public int PrestigeAvailableFromAscend() =>
    (int)Math.Floor(Math.Cbrt(TotalCookiesBaked / 1e12));

public bool Ascend() { PrestigeLevel += ...; Cookies=0; buildings.Clear(); ... }
```

`PrestigeLevel` 是唯一的转生货币，同时也是效果本身（每级 +2% CPS 全局
乘子）。没有独立的"神圣芯片"消费系统。

**理由**：
- 单指标进度 = 玩家一眼看懂 "转生 = +X% CPS 永久"
- `cbrt(baked/1e12)` 是原版沿用的近似公式，在 1e12 → 1e18 → 1e24 三段
  长度都提供有意义的收益
- 与 ADR 0002 §4 一致：`Ascend()` 是 GameState 的方法，不引入 PrestigeModule
- 保留 `AchievementSet` / `SugarLumps` / `PrestigeLevel` / `AllTimeCookiesBaked`
  跨转生持久化——这是 meta progression 的核心

**代价**：现在没法"用神圣芯片换永久点击加成"这种细粒度选择。加进来
就是补 `Dictionary<string, bool> AscendUpgrades` 一个字段的事，等真的
需要再做。

### 4. 离线收益：直接在 `SaveCoordinator` 里做

**决策**：
- 存档时用 `DateTimeOffset.UtcNow.ToUnixTimeSeconds()` 打时间戳到 `SaveData.SavedAtUnixSeconds`
- 载入时对比当前 wall-clock，把差值传给 `state.ApplyOfflineProgress(seconds)`
- `ApplyOfflineProgress` 按 50% 效率、最多 24 小时给缓存 cookies + 推进
  糖块/buff/金饼干计时
- 结果通过 `OfflineEarningsSummary` 记录，`SaveCoordinator.PendingOfflineSummary`
  给 UI 消费

**理由**：
- 24 小时上限比 ADR 0002 §7 的"截断 5 秒"体验好得多，且不会产生"离线
  16 小时爆炸产出"
- 50% 效率鼓励在线游玩，同时让 idle 玩家不完全被惩罚
- 所有计算落在 `Game.Core`：测试可写（`ApplyOfflineProgress(60)` 断言
  `earned == cps * 60 * 0.5`）
- 存档时间戳字段独立于 `GameTime`——`GameTime` 是模拟时间（不包含
  离线），`SavedAtUnixSeconds` 是真实墙钟时间，两者不冲突

**代价**：如果玩家往前拨系统时钟，能吃掉 24 小时收益。作为纯离线单机
游戏，防御这个基本没意义，忽略。

### 5. 音效：Web Audio 合成 tone，不放素材

**决策**：`wwwroot/js/cookie-clicker.js` 里用 `AudioContext` + `OscillatorNode`
合成 5 种音效（click / purchase / golden / achievement / ascend）；
C# 侧 `AudioService` 只做接口 + 静音持久化。

**理由**：
- 不用打包任何 `.mp3` / `.ogg`，仓库继续保持"零素材"
- 完全避开授权 / 归属问题（原版音效不能直接拿来用）
- 单个 `<script>` 25 行代码 vs 一堆二进制文件的可读性天差地别
- 静音状态存 localStorage（键 `cookie-clicker-remake.muted`），重新打开
  记忆

**代价**：音色是简单正弦波 + 三角波 ADSR，听起来更像 8-bit 主题的合成
乐器而不是"真的饼干碎裂声"。跟本游戏 emoji 风格一致，OK。

### 6. 粒子：CSS 变量 + `@keyframes`，没有 Canvas

**决策**：`Home.razor` 每次点击 spawn 3-5 个 `<span class="crumb">`，每个带
自己的 `--dx / --dy / --rot` CSS 变量；CSS keyframe `crumb-fly` 从中央飞出
后淡出；C# 侧只维护 `List<Crumb>` 并按时间清理（超过 900ms 移除）。

**理由**：
- 没有 canvas = 没有 raf 循环 = 没有额外 JS 交互
- CSS 变量把"每个粒子的轨迹"参数化了，DOM 里只有一个 keyframe 定义
- 走 Blazor 的 `List<>` + `StateHasChanged` 就够用，不需要独立粒子引擎
- emoji（🍪 🥠 🍫）就是天然精灵图

**代价**：单帧 5 粒 × 30 fps × 60 秒暴力点击 = 9000 元素/分钟。列表上限 60，
DOM 里始终稳定。低端机没测过。

### 7. 新闻栏：优先事件 + 落回 flavor 池

**决策**：
- `GameState` 里加 `Queue<string> _newsMessages`，成就 / 金饼干 / 糖块 / 转生
  等事件时 enqueue（"Frenzy!"、"Harvested a sugar lump!"）
- `NewsFlavor` 静态类有 20 条 ambient headline + 9 条按 `AllTimeCookiesBaked`
  分档的进度 headline
- `NewsTicker` 组件每 5-8 秒轮换：先消费 queue，空了才从池里随机拉

**理由**：
- 事件 headline 让玩家一眼确认"金饼干效果生效了"——远比 buff 图标好读
- ambient headline 让空闲期不至于死气沉沉
- 进度型 headline (`(1e18, "Astronomers redefine 'cosmic scale' after seeing your bank.")`)
  白嫖出 sense of scale，不用做剧情文案

**代价**：flavor 全英文。ADR 0002 §主动没做 里已经决定推迟 i18n。

### 8. 存档 migration：`while(Version < Current)` 步进

**决策**：`SaveSystem.Migrate` 从 switch 单次改为 while 循环步进：

```csharp
while (data.Version < CurrentVersion)
{
    switch (data.Version) { case 1: /* v1→v2 */ data.Version = 2; break; }
}
```

v1→v2 具体规则：`AllTimeCookiesBaked` 若为 0 就用 `TotalCookiesBaked` 播种；
其他 v2 新字段留默认（0 / false）；`SavedAtUnixSeconds = 0` 表示"没有时间戳，
不算离线收益"。

**理由**：
- 步进结构支持未来的 v2→v3→v4 链式升级
- 老存档不会突然拿到一大堆离线收益（因为 timestamp = 0）
- 单元测试固定一段 v1 raw JSON 断言字段迁移

**代价**：无。

---

## 主动没做的事（新一轮）

- **糖块 spending / 建筑等级**：见 §2
- **神圣芯片商店**：见 §3
- **音效素材化**：见 §5
- **rAF 驱动的粒子引擎**：见 §6
- **成就分类 tab**：现在成就格子直接堆一片，超过 200 个的时候再切
- **成就的隐藏 shadow 型**（"完成 X 后才知道条件"）：原版有，本 remake 全部
  条件在 tooltip 里可见——降低"我卡在哪"的挫败感
- **多重转生阶梯 / 神殿神秘操作**：原版极后期特色，另开里程碑

---

## 已知的限制 / 债务

- Home.razor 的粒子列表 & floater 列表都由 `OnTick` 触发 GC，量大时 JIT
  一次 clear 也 OK；未来切 `<canvas>` 时可以直接抛掉
- 音效在移动端 Safari 需要一次用户手势后才启动 `AudioContext`——
  cookie 上第一次点击本身就是一次手势，符合 Safari 的解锁要求
- `NewsFlavor` 只有 20 条 ambient，玩到中后期会重复；随时补条目即可，
  不是设计问题
- 转生动画目前只是一段音效，视觉上没有转场特效——加起来只是一个
  `.ascend-flash` overlay 的事

---

## 参考

- 项目 README：`../../README.md`
- 上一条 ADR：`0002-implementation.md`
- 原版官方浏览器版：<https://orteil.dashnet.org/cookieclicker/>
