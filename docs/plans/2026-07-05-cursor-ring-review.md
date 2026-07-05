# 光标环（Cursor Ring）代码审查

**日期**：2026-07-05
**审查范围**：光标环特性新增改动
**设计文档**：`docs/plans/2026-07-05-cursor-ring-design.md`

## 审查文件

- `src/Game.Web/Components/CursorRingLayout.cs`（新增，纯派生算法）
- `src/Game.Web/Components/CursorRing.razor`（新增，组件）
- `src/Game.Web/Components/CursorRing.razor.css`（新增，样式与动画）
- `src/Game.Web/Pages/Home.razor`（接入 CursorRing）
- `src/Game.Core/Localization/TranslationsEn.cs`、`TranslationsZhHans.cs`、
  `TranslationsZhHant.cs`（新增 `ui.cursorring.*` 文案）
- `tests/Game.Web.Tests/CursorRingLayoutTests.cs`（新增测试）

## 统计

- 已审查文件：7
- 发现问题：1（P1）
- 待复查项：0
- 已全部修复

## 问题列表

### P1-1：光标环每帧重建 160 个 DOM（已修复）

**位置**：`CursorRing.razor`（组件未实现 `ShouldRender`）

**问题**：`Home.razor` 每 tick（~30fps）触发 `StateHasChanged`，`CursorRing`
接收同一被原地 mutate 的 `GameState` 引用，Blazor 无法判断变化，默认每帧重渲染
子组件。导致 `CursorRingLayout.Compute` 每帧分配最多 160 个 `Finger` 的 List，
且 160 个 `<div>`+`<svg>` 每帧参与 diff（约 4800 次元素 diff/秒）。而光标环仅在
购买 Cursor 时变化，绝大多数帧为无谓开销。

**后果**：低端设备持续无谓 diff 可能掉帧——恰是本设计（颜色合并锁死 DOM 数量）
想规避的渲染成本。

**修复**：给 `CursorRing` 实现 `ShouldRender`，缓存上一帧渲染时的 Cursor 数量
（`_renderedCount`），仅当数量变化时才重渲染。tooltip 的实时刷新由 `TooltipHost`
自身的 tick 订阅驱动（独立重跑 builder），不依赖本组件每帧重渲染，故契约不受
影响。

## 已确认的非问题

- **溢出安全**：`long.MaxValue` 曾触发 `CeilDiv` 的 `a+b-1` 溢出，实现阶段测试
  已捕获并改为不溢出写法，测试 `AstronomicalCount_ClampsToGoldCeiling` 守护。
- **空引用**：`@ref _ring` 仅在 `Fingers.Count > 0`（对应 div 已渲染）时被
  `ShowTooltip` 使用，无空引用风险。
- **pointer-events**：容器 `pointer-events:none`（不挡饼干点击），仅手指
  `pointer-events:auto`（可 hover 出 tooltip），权衡正确。
- **翻译文件**：纯文案增补，3 语言 key 对齐，无逻辑。

## 验证

- `dotnet build Game.slnx -c Release`：0 警告 0 错误
- `dotnet test`：Game.Web.Tests 18 通过，Game.Core.Tests 107 通过
