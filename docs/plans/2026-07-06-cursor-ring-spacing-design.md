# 光标环间距优化设计

日期：2026-07-06
状态：已确认，待实现

## 背景

Cursor 转圈时，相邻同心环之间靠得太近，视觉上手指互相压叠。

根本原因：手指盒子高 `2.3rem`（锚点居中，向内/向外各占约 1.15rem），而环间距
`RingGapRem = 1.0rem`。相邻两环手指实际互相压进约 1.3rem，没有任何缝隙。

## 目标

手指**整体缩小一号**，并**加大环间距到不挤为止**，让每一圈手指之间留出清晰缝隙。

## 取舍决定

- **不做**「越往外手指越小」的递减缩放——手指统一小一号即可。
- 加大环距会让最外环半径变大（约 12.5rem → 17rem）。`.cookie-area` 有
  `overflow: hidden`，满 5 环（约 ≥100 个光标）时窄屏可能裁掉外圈边缘。
  **决定：接受满环时外圈被裁，不放开 `overflow`。**

## 改动

### A. 手指整体缩小（`CursorRing.razor.css`，等比约 82%）

| 属性 | 现在 | 改后 |
|---|---|---|
| `.cursor-finger` width | 1.4rem | 1.15rem |
| `.cursor-finger` height | 2.3rem | 1.9rem |
| poke `@keyframes` translateY | 0.5rem | 0.4rem |

### B. 加大环间距（`CursorRingLayout.cs`，只动常量）

- `RingGapRem`: `1.0 → 2.2`
- `BaseRadiusRem`: `8.5 → 8.2`（手指变小，最内圈略收以贴饼干）
- 净效果：手指盒高 1.9rem，环距 2.2rem，相邻环留约 0.3rem 净缝隙。
- 最外环半径约 `8.2 + 4×2.2 ≈ 17rem`，满环时窄屏可能被裁（按决定接受）。

### C. 移动端同步（`@media max-width: 900px`）

- 手指等比缩到约 `0.82rem × 1.35rem`（保持与桌面一致比例）。
- radius 仍乘 `0.66`，环距随之 ×0.66 ≈ 1.45rem，与缩小后手指高约 1.3rem 仍留正缝隙。

## 不改动

`Compute` / `BuildFingers` / tier 颜色合并逻辑、`RingCapacities`、`MaxFingers`。
现有 `CursorRingLayoutTests` 只断言环容量与合并阈值，不碰半径/间距数字——全部不受影响。
