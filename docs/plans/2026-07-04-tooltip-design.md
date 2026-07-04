# 富 Tooltip 设计（建筑 / 升级 / 成就）

- 日期：2026-07-04
- 状态：设计已确认，待实施
- 范围：为建筑、升级、成就三处引入接近原版 Cookie Clicker 的自定义富 Tooltip，替换现有原生 `title` 属性

## 背景与动机

当前 `BuildingRow`、`UpgradeStore`、`AchievementList` 三处均使用原生 HTML `title` 属性做简易提示。原生 `title` 的问题：

- 出现有 ~1s 延迟，样式无法定制，不支持富文本 / 多行布局
- 移动端不可用
- 无法展示实时衍生数据（如某建筑占全局产量的百分比、回本时间）

原版 Cookie Clicker 的 Tooltip 是招牌体验之一：跟随鼠标、富样式、信息密集。三处统一替换为自定义富 Tooltip，可显著提升购买决策体验与视觉一致性。

## 决策记录

- **范围**：三处全做（建筑 + 升级 + 成就），共享同一套组件。
- **技术方案**：顶层共享 Tooltip 宿主 + 鼠标跟随。符合项目"逻辑在 C#、极少 JS"的风格，且 `fixed` 定位可避免被滚动容器 / 滚动条裁剪。
- **信息密度**：信息密集（接近原版）。衍生数据（占比、回本时间）在 UI 层用已有 `GameState` 方法现算，`Game.Core` 零改动。
- **内容组织**：统一的 `TooltipCard.razor` 组件（DRY），通用"标题 + 信息行 + 脚注"参数模型。
- **成就解锁时间**：不做。`UnlockedAchievements` 仅为 `HashSet<string>`，不记录时间戳；为遵守 YAGNI 不改动存档结构。成就 Tooltip 仅展示名称/描述/解锁状态。

## 架构

顶层单例 Tooltip 宿主，由各调用点按需推送内容。

1. **`TooltipService`（Scoped，DI 注入）** — 持有当前状态：`Visible`、要渲染的 `RenderFragment? Content`。提供 `Show(RenderFragment)` / `Hide()`，状态变化触发 `OnChange` 事件。
2. **`TooltipHost.razor`（`MainLayout` 根部，全局唯一）** — 订阅 `OnChange`，把内容渲染进 `position: fixed` 的浮层，永不被滚动容器裁剪。
3. **鼠标坐标** — 极简 JS interop：`document` 上挂 `mousemove` 缓存 `clientX/clientY`；显示时读取一次做定位与边界翻转（靠近右/下边缘时向左/上展开）。

**数据流**：各 tile/row 在 `@onmouseover` 时调用 `TooltipService.Show(内容)`，`@onmouseout` 调用 `Hide()`。内容里的实时衍生数据在构建 `RenderFragment` 时从 `GameState` 现算。

## 组件设计

### JS（追加到现有 `wwwroot/js/cookie-clicker.js`，不新建文件）

复用 `window.cookieClicker` 命名空间，风格与现有音频函数一致：

```js
let mx = 0, my = 0;
window.cookieClicker.initTooltip = function () {
    document.addEventListener('mousemove', e => { mx = e.clientX; my = e.clientY; });
};
window.cookieClicker.positionTooltip = function (el) {
    const pad = 16, w = el.offsetWidth, h = el.offsetHeight;
    let x = mx + pad, y = my + pad;
    if (x + w > window.innerWidth)  x = mx - w - pad;
    if (y + h > window.innerHeight) y = my - h - pad;
    el.style.left = x + 'px';
    el.style.top  = y + 'px';
};
```

`TooltipHost` 在 `OnAfterRenderAsync(firstRender)` 调用一次 `initTooltip()`，之后每次显示调用 `positionTooltip(_el)`。

### TooltipService.cs（`src/Game.Web/Services/`）

```csharp
public sealed class TooltipService
{
    public RenderFragment? Content { get; private set; }
    public bool Visible { get; private set; }
    public event Action? OnChange;

    public void Show(RenderFragment content)
    {
        Content = content;
        Visible = true;
        OnChange?.Invoke();
    }

    public void Hide()
    {
        Visible = false;
        OnChange?.Invoke();
    }
}
```

`Program.cs` 注册 `builder.Services.AddScoped<TooltipService>()`（WASM 下 Scoped 等同单例，与现有服务风格一致）。

### TooltipHost.razor

- 注入 `TooltipService` 和 `IJSRuntime`，订阅 `OnChange`（`IDisposable` 退订）。
- 结构：

  ```razor
  @if (Ts.Visible && Ts.Content is not null)
  {
      <div class="tooltip-layer" @ref="_el">
          @Ts.Content
      </div>
  }
  ```

- `OnChange` 触发 `InvokeAsync(StateHasChanged)`。
- 可见后在 `OnAfterRenderAsync` 调用 JS `positionTooltip(_el)`。
- `.tooltip-layer`：`position: fixed; pointer-events: none; z-index: 极高`——`pointer-events: none` 防止 tooltip 吃鼠标事件导致闪烁。

### TooltipCard.razor（`src/Game.Web/Components/`）

通用"标题 + 信息行 + 脚注"结构：

```razor
<div class="tooltip-card @CssClass">
    <div class="tooltip-head">
        <span class="tooltip-icon">@Icon</span>
        <span class="tooltip-title">@Title</span>
        @if (Tag is not null) { <span class="tooltip-tag">@Tag</span> }
    </div>
    @if (Rows is not null)
    {
        <div class="tooltip-rows">
            @foreach (var (label, value) in Rows)
            {
                <div class="tooltip-row">
                    <span class="tooltip-row-label">@label</span>
                    <span class="tooltip-row-value">@value</span>
                </div>
            }
        </div>
    }
    @if (!string.IsNullOrEmpty(Flavor))
    {
        <div class="tooltip-flavor">@Flavor</div>
    }
</div>

@code {
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? Tag { get; set; }
    [Parameter] public IReadOnlyList<(string, string)>? Rows { get; set; }
    [Parameter] public string? Flavor { get; set; }
    [Parameter] public string? CssClass { get; set; }
}
```

## 各处 Tooltip 内容

各组件移除原生 `title`，改为 `@onmouseover`/`@onmouseout` 调用 `TooltipService`，内容构造为 `RenderFragment` 包 `<TooltipCard />`。衍生数值在构造 Rows 时现算并用 `NumberFormat` 格式化。

### 建筑（BuildingRow）— 信息最密集

- 头部：图标 + 名称
- 成本：`NextBuildingCost`（单价）；bulk 时附 `× N` 总价
- 拥有 N 个
- 每个 cps `GetBuildingUnitCps` + 该类总 cps `GetBuildingTotalCps`
- **占全局 cps 百分比**：`GetBuildingTotalCps(id) / State.CurrentCps()`（除零保护）
- **回本时间**：`unitCost / unitCps` 秒
- 脚注：斜体风味文字 `FlavorText`

### 升级（UpgradeStore tile）

- Tag：类别 `Category`
- 头部：图标 + 名称
- Rows：效果描述 `Description`、成本
- 可选量化效果："×2 光标产量"，由 `EffectKind` + `EffectValue` 拼

### 成就（AchievementList tile）

- 已解锁：图标 + 名称 + 描述（Flavor）
- 未解锁：🔒 + `Title="???"`（保持神秘，与原版一致）

## 落地清单

1. `cookie-clicker.js` — 追加 `initTooltip` / `positionTooltip`
2. `TooltipService.cs` — 新建，`Program.cs` 注册 Scoped
3. `TooltipHost.razor` — 新建，放进 `MainLayout` 根部
4. `TooltipCard.razor` — 新建，通用卡片
5. `BuildingRow.razor` / `UpgradeStore.razor` / `AchievementList.razor` — 移除 `title`，加 `@onmouseover`/`@onmouseout`
6. CSS — `.tooltip-layer`（fixed 层）+ `.tooltip-card` 及三处主题色，加进现有站点样式
7. 移动端：无 hover，Tooltip 自然不触发，移除 `title` 不影响——可接受

## 非目标（YAGNI）

- 不记录成就解锁时间戳，不改动 `SaveData` / 序列化
- 不为衍生计算给 `Game.Core` 新增方法（复用现有 `GetBuildingUnitCps`/`GetBuildingTotalCps`/`CurrentCps`/`NextBuildingCost`）
- 不引入 JS 定位库（Floating UI / Popper）
- 不做移动端的长按 Tooltip
