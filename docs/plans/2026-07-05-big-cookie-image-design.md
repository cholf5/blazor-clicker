# 大饼干改用图片资源 · 设计

**日期**：2026-07-05
**背景**：光标环没有完美围住大饼干。根因是大饼干是 emoji（🍪）字形，
其可见像素并不居中于文字盒（有基线/字形内边距偏移，且跨平台/字体不一致），
导致「盒子中心 ≠ 视觉中心」，而环靠 `cookie-area` 的居中定位，两者几何中心
本应重合却因此偏移。

**方案**：用自绘（AI 重绘、非原版、无版权问题）的 500×500 透明 PNG 替代 emoji。
图片的可见像素居中于画布，几何中心 = 视觉中心，环自然同心。

## 第 1 部分：替换 emoji 为图片

- **资源**：`src/Game.Web/wwwroot/img/big-cookie.png`（500×500，Alpha 透明，
  饼干可见部分约占画布 85%，居中）。
- **`BigCookie.razor`**：把 `<span class="big-cookie-emoji">🍪</span>` 换成
  `<img class="big-cookie-img" src="img/big-cookie.png" alt="@Loc["ui.cookie.aria"]"
  draggable="false" />`。
  - `src` 用相对路径 `img/big-cookie.png`（无前导 `/`），保证 GitHub Pages
    子路径 `<base href>` 重写生效。
  - `draggable="false"` 防止拖拽产生幽灵图。
- **`BigCookie.razor.css`**：`.big-cookie-emoji { font-size:15rem }` →
  `.big-cookie-img { width:15rem; height:15rem; display:block; }`，移动端
  `@media (max-width:900px)` 同步 15rem→10rem。`.big-cookie` 的 drop-shadow、
  hover/active 缩放、bounce 动画不变。

## 第 2 部分：校准光标环半径

- emoji 时代 `BaseRadiusRem = 7.6` 是按「emoji 半径 ≈ 7.5rem」估的，偏大且不准。
- 图片宽 15rem → 半径 7.5rem，饼干可见部分约占 85% → 边缘半径 ≈ 6.4rem。
  内圈手指贴边缘外一点。
- **`CursorRingLayout.cs`**：`BaseRadiusRem` 7.6 → **6.6**（视觉初值，浏览器内
  肉眼微调）。同步更新过时的「emoji radius ≈ 7.5rem」注释。`RingGapRem` 不变。
- **移动端**：`CursorRing.razor.css` 用 `translateY(calc(-0.66 * var(--radius)))`
  缩放，`10/15 ≈ 0.667`，桌面/移动缩放比与图片一致，无需改动。

## 测试影响

`CursorRingLayoutTests` 只断言手指数量与颜色层级，不断言 `RadiusRem` 具体值，
改半径常量不破坏任何测试。半径为纯视觉参数，最终以浏览器实测为准。
