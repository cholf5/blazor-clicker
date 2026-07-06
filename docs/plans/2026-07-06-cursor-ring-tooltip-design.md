# 光标环 tooltip 恢复设计

- 日期：2026-07-06
- 状态：已验证，待实施
- 范围：`CursorRing.razor`(+ `.css`)、`StatsPanel.razor`(+ `.css`)。领域层不动。

## 背景与动机

玩家反馈：大饼干的光标环、以及 StatsPanel 里的「光标: N → M / ✓」控件
都缺少有意义的说明，玩家看不懂 `→ N` / `✓` 是什么意思。

事实核对（澄清一个常见误记）：

- **牛奶 tooltip 从未被删**，`MilkLayer.razor:15-62` 现有完整富 tooltip，工作正常。
- 真正被移除的是**光标环 tooltip**，见 commit
  `86b7060 fix: Move cursor ring status from tooltip to stats panel`。当时把光标
  状态从 tooltip 挪进了 StatsPanel。
- 用户口中「大饼干上加的光标控件」其实在 StatsPanel(`StatsPanel.razor:28-42`），
  不在大饼干上。

用户首选：**把稳定的 tooltip 做回光标环上**。

## 根因：触发 hover 的元素 = 定位锚点，而它在旋转

TooltipService 的定位读**锚点元素的 bounding rect**，把卡片摆在它旁边
(`TooltipHost.razor:43`）。当年放弃的原因链：

- 外层 `.cursor-ring` 铺满 `.cookie-area`(`inset:0`)且 `pointer-events:none`，
  **收不到 hover**。
- 唯一 `pointer-events:auto` 的是**每一根手指**，而手指挂在 60s 旋转的
  `.cursor-ring-spinner` 里。
- 于是 tooltip 只能锚到手指 → 锚点跟着手指转 → 卡片「追着手指跑」。
- `9a26c88` 想用「外层静止容器当锚点」解决，但外层 `pointer-events:none` 收不到
  hover，方案没落地，`86b7060` 撤除。

**解法**：把「触发 hover 的元素」与「用作定位的锚点元素」**合一到一个静止元素**上。
这一点当年没做到，不代表做不到。

## 设计

### 1. 命中环几何与层叠

在光标环所在的环形带上叠一层**静止、可 hover、不挡饼干点击**的透明命中环，
既触发 tooltip 又作定位锚点。

几何（来自 `CursorRingLayout`）：

- 手指锚点半径 `8.5rem`(内环）到 `12.5rem`(外环，`BaseRadiusRem 8.5 + 4×RingGapRem`）。
- 手指盒向内还伸 ~1.15rem，poke 再 ~0.5rem，故手指实际占据 **~7.5rem 到 ~12.5rem** 环带。
- 中心大饼干半径 7.5rem(`15rem/2`）。移动端整体 ×0.66（饼干 10rem）。

层叠结构：

```
.cursor-ring        ← inset:0, pointer-events:none（不变，静止）
├─ .cursor-ring-hit ← 新增：静止圆盘，pointer-events:auto，收 hover + 当锚点
└─ .cursor-ring-spinner ← 旋转层，手指（不变）
```

- `.cursor-ring-hit` 固定为环带外径(≈ `25rem` 直径），`border-radius:50%`，居中。
- **中心镂空**（甜甜圈）：用 CSS `mask`/`clip-path` 挖空中心 15rem 直径圆，把中心
  区域的 `pointer-events` 让给饼干，保证点击饼干不被拦。（用户已认可 CSS 遮罩方案。）
- 因静止，作为锚点时 `getBoundingClientRect` 恒定，tooltip 不追手指。卡片摆在环
  右侧，沿用统一的「右侧优先 + 翻转」规则（不改现有定位策略）。

### 2. 组件改动与 tooltip 接线

改动集中在 `CursorRing.razor`(+ `.css`）。恢复被 `86b7060` 删掉的部分（git 现成）：

- `@inject TooltipService Tooltip`
- 锚点字段 `private ElementReference _hit;`
- `BuildTooltip()` 方法原样恢复。它已算好 `owned / tier / per_finger / next_merge / flavor`，
  且 loc key `ui.cursorring.*` 三种语言全在，**零文案工作**。

接线（与牛奶、建筑同款 builder 现算契约）：

```razor
<div class="cursor-ring" aria-hidden="true">
    <div class="cursor-ring-hit"
         @ref="_hit"
         @onmouseover="ShowTooltip"
         @onmouseout="Tooltip.Hide"></div>
    <div class="cursor-ring-spinner"> …手指… </div>
</div>

private void ShowTooltip() => Tooltip.Show(BuildTooltip, _hit);
```

关键差异 vs 当年：锚点从「每根旋转的手指」换成「静止的 `.cursor-ring-hit`」。
这就是根因的解法——触发元素与锚点合一，且该元素静止。

注意点：

- **ShouldRender gate 保留**：`CursorRing` 现按 cursor 数量 gate 重渲。这不影响
  tooltip 实时刷新——`TooltipHost` 自己订阅 `OnTick` 每帧重跑 builder
  (`TooltipHost.razor:54`），live 值走 host 那条线，不依赖本组件重渲。
- **不踩 keyless-foreach 陷阱**：命中环恒定存在（与 `.cursor-ring` 同生命周期），
  非动态增删，不需要 `@key` / 死 ref 清理。JS 定位层已有 try/catch 兜底。

### 3. StatsPanel 清理（信息只在环上一处呈现）

信息回到环上后，StatsPanel 的光标行冗余，删除：

- `StatsPanel.razor:28-42` 的 `@if (cursors > 0){…}` 块。
- `StatsPanel.razor:11-12` 派生的 `cursors` / `cursorModel` 两行及注释；若无其他引用，
  清掉相关 `@using` / `CursorRingLayout` 引用。
- `StatsPanel.razor.css` 中 `.stats-cursors` / `.tier-dot` / `.merge-progress`
  规则（已确认这些类仅在此 scoped css 内使用；`CursorRing` 用的是独立的
  `.cursor-finger.tier-N`，不受影响）。
- **保留** loc key `ui.cursorring.*`——现归 tooltip 使用。
- `CursorRingLayout` 仍被 `CursorRing.razor` 及其单测使用，**保留**。

### 边界情况

- **空环**：0 光标时整个 `.cursor-ring` 不渲染，命中环随之不存在 → 无 tooltip，符合直觉。
- **移动端**：`@media (max-width: 900px)` 下命中环外径与中心镂空半径挂同一套等比
  缩小（×0.66），否则镂空对不上饼干。本节唯一需小心的 CSS 细节。
- **触摸设备**：`@onmouseover` 纯触摸屏不触发——这是全站既有行为（牛奶/建筑 tooltip
  同样靠 hover），保持一致，不在本次扩展。
- **reduced-motion**：命中环本就静止，无需改动。

## 测试与验证

- 布局逻辑(`CursorRingLayout.Compute`）已有纯单测覆盖，BuildTooltip 只消费它，
  不新增领域测试。
- Web 测试守护「tooltip 用 builder 现算、不退回缓存 fragment」的契约：若 `86b7060`
  当年连带删了对应 Web 测试则恢复它；否则补一个与牛奶测试同款的断言
  (`CursorRing` 的 `Tooltip.Show` 收到 `Func<RenderFragment>`）。
- 人工验证：`dotnet build`(0 warning）→ `dotnet watch` → hover 光标环确认卡片
  稳定出现在环右侧、不追手指、点饼干不被拦、live 值随买光标刷新。
- **全部前台运行**（仓库铁律：绝不后台跑 test/build/watch）。

## 验收标准

- tooltip 稳定出现在大饼干的光标环上，不随手指旋转移动。
- 中心饼干点击不受影响。
- StatsPanel 不再显示光标行。
- 0 warning / 0 error；测试通过。
