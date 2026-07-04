# 建筑渐进解锁 —— 代码审查

- 日期：2026-07-04
- 范围：本次「建筑渐进解锁」功能改动
- 结论：**通过**（发现 2 个问题，均已当场修复并回归验证）

## 审查文件

- `src/Game.Core/Domain/ProgressionConfig.cs`
- `src/Game.Core/Domain/GameState.cs`（`IsBuildingUnlocked` / `NextLockedBuilding` / `BuildingUnlockThreshold`）
- `src/Game.Web/Components/BuildingList.razor`
- `src/Game.Web/Components/BuildingRow.razor`
- `src/Game.Web/Components/BuildingRow.razor.css`
- `tests/Game.Core.Tests/BuildingUnlockTests.cs`

## 问题列表

### P1-1 解锁阈值公式重复，存在静默不一致风险（已修复）

**位置**：`GameState.IsBuildingUnlocked` 与 `BuildingRow.razor`

**问题**：解锁阈值 `BaseCost × BuildingUnlockCostFraction` 在域层判定和 UI
「Unlocks at N baked」提示两处各写一遍。两值本应恒等；一旦公式演进（非线性、
叠加前置数量等），只改一处会让 UI 显示与真实解锁条件静默不一致。

**修复**：在 `GameState` 引入单一真源
`public double BuildingUnlockThreshold(BuildingId id)`。域层判定、UI 提示、测试
期望值计算全部改为调用它。

### P2-2 未知建筑 id 被静默当作已解锁（已修复）

**位置**：`GameState.IsBuildingUnlocked`（原 `if (index <= 0) return true;`）

**问题**：不在 `Buildings.All` 中的 id（`index == -1`）与 Cursor（`index == 0`）
被合并处理，返回「已解锁」。未知 id 属预期外的编程错误，不应被静默掩盖。

**修复**：`index < 0` 时抛 `ArgumentOutOfRangeException`；`index == 0`（Cursor）
单独返回 true。

## 已确认的非问题

- 无。

## 验证

- `dotnet test Game.slnx -c Release`：72 通过 / 0 失败
- `dotnet build src/Game.Web -c Release`：0 警告 / 0 错误
