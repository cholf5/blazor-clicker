# 建筑渐进解锁机制设计

- 日期：2026-07-04
- 状态：已验证，待实施

## 背景

原版 Cookie Clicker 不会一次性把全部建筑展示给玩家，而是随进度逐渐解锁：
只要玩家**拥有前一种建筑**、并且**累计烘焙的饼干接近该建筑价格**时，下一种
建筑才会出现在商店里。当前 remake 版缺失这套机制——`BuildingList.razor` 直接
`foreach (Buildings.All)` 无条件渲染全部 18 种建筑。本设计把渐进解锁加回来。

## 解锁规则

一个建筑 X 是否**已解锁（visible + 可购买）**，取决于两个条件**同时**满足：

1. **前置**：X 在 `Buildings.All` 顺序里的前一种建筑已拥有 ≥ 1 个。
   第一种建筑 Cursor 无前置，永远解锁。
2. **阈值**：本轮 `TotalCookiesBaked` ≥ `X.BaseCost * BuildingUnlockCostFraction`。
   系数取 `0.5`（略小于 1，让玩家在快买得起时就看到它，贴近原版手感）。

### 为什么用 TotalCookiesBaked 而非当前 Cookies 余额

`TotalCookiesBaked` 本轮只增不减，前置的「拥有数」也不会因购买后续建筑而减少。
两者结合天然保证**解锁不可反悔**：买东西花掉饼干、余额下降，不会让已解锁的
建筑消失。因此**不需要持久化「已解锁集合」**——每帧从既有 state 纯计算即可，
符合项目「UI 轮询、按需计算」的既定风格。

## 域层改动（Game.Core）

### ProgressionConfig.cs

新增常量：

```csharp
/// <summary>建筑解锁门槛 = 该建筑 BaseCost 的这一比例（本轮 TotalCookiesBaked 需越过）。</summary>
public const double BuildingUnlockCostFraction = 0.5;
```

### GameState.cs

新增两个纯查询方法（无副作用，按需计算）：

```csharp
// 已解锁：应正常显示、可购买
public bool IsBuildingUnlocked(BuildingId id);
// 下一个即将解锁的建筑（用于占位行）——第一个未解锁的建筑，或 null（全部解锁时）
public BuildingId? NextLockedBuilding();
```

`IsBuildingUnlocked(id)` 逻辑：定位 id 在 `Buildings.All` 的下标 `i`；
`i == 0` 直接 true；否则要求
`BuildingCounts[All[i-1].Id] >= 1` **且**
`TotalCookiesBaked >= All[i].BaseCost * BuildingUnlockCostFraction`。

在 `BuyBuilding(id)` 中**不加**解锁校验。渐进解锁是**展示层行为**（商店里
出不出现 / 是否禁用），而非经济硬约束——正常游玩时前置 + 阈值在买得起之前
就已满足，UI 隐藏/禁用即可。把校验塞进 `BuyBuilding` 会把 16 处「拿建筑当
测试 fixture」的既有测试打回（其中若干还会因 CPS 恒 0 令 `while(baked<X)Tick()`
死循环），且偏离原版语义。因此 `IsBuildingUnlocked` 仅供 UI 查询，域层购买路径
保持不变。

## UI 改动（Game.Web）

### BuildingList.razor

不再无条件遍历全部建筑：

```razor
@foreach (var def in Buildings.All.Where(d => State.IsBuildingUnlocked(d.Id)))
{
    <BuildingRow ... />   @* 正常行 *@
}
@{ var next = State.NextLockedBuilding(); }
@if (next is { } nextId)
{
    <BuildingRow State="State" Definition="Buildings.Get(nextId)" Locked="true" />
}
```

即：已解锁建筑正常渲染；末尾额外渲染**一个**「下一个」占位行（若存在）；
再往后的建筑完全不出现。列表长度随进度自然增长。

### BuildingRow.razor

新增 `[Parameter] bool Locked`。当 `Locked == true` 时：

- 图标暗色/降透明度，名字显示为 `???`（不泄露名称与 flavor）。
- 成本行改为解锁提示，如 `Unlocks at 6.5M baked`（格式化该建筑门槛
  `BaseCost * fraction`），让玩家知道还差多少。
- `disabled` 恒为 true，`@onclick` 不触发购买。
- tooltip 显示通用文案「Keep baking to reveal the next building.」，不泄露真实数据。

复用同一组件，`Locked` 分支用 CSS class `building-row locked-preview` 控制灰显。

## 存档与迁移

**不新增任何持久化字段**。解锁状态由已持久化的 `TotalCookiesBaked` +
`BuildingCounts` 推导得出，因此：

- **不改 `SaveData` 形状**，`CurrentVersion` 保持 **3**，**无需新增迁移 case**。
- 老存档加载后由查询方法重新计算，行为正确。已拥有（数量 ≥ 1）的建筑其门槛必
  然早已越过，不会被「藏回去」。

## 测试（tests/Game.Core.Tests/BuildingUnlockTests.cs）

纯域层，风格对齐现有 `PurchaseTests` / `ProgressionTests`：

1. **初始状态**：全新 state 仅 Cursor 解锁；`NextLockedBuilding()` 返回 Grandma；
   Farm 及之后未解锁。
2. **前置未满足**：`TotalCookiesBaked` 拉到极高但未买 Grandma，Farm 仍不解锁。
3. **阈值未满足**：拥有 Cursor 但烘焙量低于 Grandma 门槛，Grandma 未解锁。
4. **两条件齐备**：买 1 个 Cursor + 越过门槛 → Grandma 解锁，`NextLockedBuilding()`
   前移到 Farm。
5. **展示层语义（非硬约束）**：对未解锁建筑 `IsBuildingUnlocked` 返回 false，
   但域层 `BuyBuilding` 仍允许购买成功（解锁只驱动 UI 显示/禁用，不拦截购买）。
6. **不可反悔**：解锁后花光 `Cookies`（余额归零，`TotalCookiesBaked` 不变）→
   建筑仍解锁。
7. **老存档兼容**：构造 version-3、`BuildingCounts` 含中期建筑的 `SaveData`，
   `Load` 后这些已拥有建筑仍判定为解锁。

## 改动清单

- `ProgressionConfig.cs`：新增 `BuildingUnlockCostFraction = 0.5`
- `GameState.cs`：新增 `IsBuildingUnlocked` / `NextLockedBuilding`（`BuyBuilding` 不动，解锁仅作展示层信号）
- `BuildingList.razor`：过滤已解锁 + 渲染单个占位行
- `BuildingRow.razor`：新增 `Locked` 参数及灰显/神秘分支
- CSS：新增 `.locked-preview` 样式
- `BuildingUnlockTests.cs`：7 组测试
- **不动存档版本/迁移**
