# 存档文件导入导出设计

- 日期：2026-07-05
- 状态：已实施
- 更新：2026-07-05 —— 统一导出/导入交互（见「交互修订」一节）

## 目标

在现有"剪贴板"导入导出通道之外，再增加一条**文件通道**：

- **导出到文件**：将存档下载为 `.txt` 文件。
- **从文件导入**：选择一个 `.txt` 文件读取存档。

两条通道共用同一份存档字符串（`SaveSystem.ExportToString` 产出的 Base64），
因此文件内容与剪贴板内容**完全一致**、可互相粘贴，不引入任何新的核心逻辑。

## 约束与原则

- 遵守 UI → domain 单向依赖：文件读写属于浏览器 IO，只在 `Game.Web` 层，
  不进 `Game.Core`。
- 严格 YAGNI：不为"字符串搬运方式"新增核心测试。

## 交互（最终形态）

导出与导入采用**对称交互**：数据区顶层只有 4 个按钮（保存 / 导出 / 导入 / 清除），
点击「导出」或「导入」都在下方展开**同一套控件**（文本框 + 两个操作按钮 +
「或 …」链接式副操作），两个面板互斥，再次点击同一按钮收起。

- **导出面板**：只读文本框预填 Base64 存档串；文本框上方为「**或 [导出到文件]**」
  链接按钮（触发文件下载）；下方操作按钮为「**拷贝**」（复制到剪贴板）/「取消」。
- **导入面板**：可编辑文本框供粘贴；文本框上方为「**或从文件选择：[选择文件]**」
  （`<InputFile>`）；下方操作按钮为「**载入**」/「取消」；错误在文本框下展示。

状态用单一 `_panel`（`None` / `Export` / `Import`）管理，而非独立布尔开关。

## 设计

### 1. 导出为文件

**JS 侧**（`src/Game.Web/wwwroot/js/cookie-clicker.js`）新增函数，风格与现有
`getBrowserLanguage` / `positionTooltip` 一致（纯函数、挂 `window.cookieClicker`
命名空间、无新依赖）：

```js
window.cookieClicker.downloadTextFile = function (filename, text) {
    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
};
```

**C# 侧**（`OptionsMenu.razor`）—— 展开导出面板时把 `Save.Export()` 存入
`_exportText`，供文本框显示、「拷贝」与「导出到文件」共用同一份串：

```csharp
private void ToggleExport()
{
    if (_panel == SavePanel.Export) { ClosePanel(); return; }
    _exportText = Save.Export();
    _importError = null;
    _panel = SavePanel.Export;
}

private async Task OnCopyExport()
{
    try
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", _exportText);
        FlashToast(Loc["ui.save.exported"]);
    }
    catch { FlashToast(Loc["ui.save.export_manual"]); }
}

private async Task OnExportFile()
{
    var name = $"cookie-clicker-save-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
    await JS.InvokeVoidAsync("cookieClicker.downloadTextFile", name, _exportText);
    FlashToast(Loc["ui.save.exported_file"]);
}
```

- 文件名：`cookie-clicker-save-<本地时间戳>.txt`，时间戳用本地时间
  （`DateTime.Now`），避免多次导出互相覆盖。
- 文件内容 = `_exportText`，与剪贴板通道一致。

### 2. 从文件导入

**UI** — 导入面板内、textarea **上方**加一行文件选择，用 Blazor 原生
`<InputFile>`（需 `@using Microsoft.AspNetCore.Components.Forms`）：

```razor
<div class="save-import">
    <label class="save-import-file">
        @Loc["ui.save.import_file"]
        <InputFile OnChange="OnImportFile" accept=".txt" />
    </label>
    <textarea @bind="_importText" placeholder="@Loc["ui.save.import_placeholder"]"></textarea>
    <div class="save-import-actions"> … Load / Cancel … </div>
    @if (!string.IsNullOrEmpty(_importError)) { <div class="save-error">@_importError</div> }
</div>
```

**读取逻辑** — 读成字符串后汇入现有 `Save.ImportAsync`，与粘贴走同一条校验/报错路径：

```csharp
private async Task OnImportFile(InputFileChangeEventArgs e)
{
    _importError = null;
    try
    {
        using var reader = new StreamReader(e.File.OpenReadStream(maxAllowedSize: 2 * 1024 * 1024));
        var content = await reader.ReadToEndAsync();
        await Save.ImportAsync(content);
        ClosePanel();
        _importText = "";
        await OnStateChanged.InvokeAsync();
        FlashToast(Loc["ui.save.loaded"]);
    }
    catch (Exception ex)
    {
        _importError = Loc.Format("ui.save.import_error", ex.Message);
    }
}
```

- `maxAllowedSize` = 2MB（存档实际仅几 KB，2MB 为安全上限）。
- 成功后与粘贴导入一致：收起面板、清空、刷新、提示。
- 错误（非存档文件、Base64 解不开）复用 `_importError` 展示，不新增机制。

### 3. 本地化

三份 `Translations*.cs`（En / ZhHans / ZhHant）新增键：

| 键 | En | ZhHans | ZhHant |
|---|---|---|---|
| `ui.save.export_file` | Export to file | 导出到文件 | 匯出到檔案 |
| `ui.save.exported_file` | Save exported to file. | 存档已导出到文件。 | 存檔已匯出到檔案。 |
| `ui.save.import_file` | Or load from a file: | 或从文件选择： | 或從檔案選擇： |
| `ui.save.copy` | Copy | 拷贝 | 拷貝 |
| `ui.save.or_prefix` | Or | 或 | 或 |

## 交互修订（2026-07-05）

初版曾在数据区顶层并排放置 `📁 导出到文件` 按钮，导致「导出」（直接复制到剪贴板）
与「导入」（展开面板）交互不一致。修订后：

1. 取消顶层「导出到文件」按钮。
2. 「导出」改为展开与「导入」对称的面板：只读文本框预填 Base64 串，下方「载入」
   位对应改为「**拷贝**」。
3. 文件通道降为面板内的「**或 [导出到文件]**」链接式副操作，与导入侧的
   「或从文件选择」对称。

废弃键 `ui.save.export_file_title` 已删除。

## 测试

**不新增核心测试。** 文件通道与交互改动不引入新的核心逻辑：

- 导出内容 = `SaveSystem.ExportToString`（已有测试覆盖）。
- 导入 = `SaveSystem.ImportFromString`（已有测试覆盖）。

文件读写与面板切换只是把字符串搬运方式/展示方式改在 UI 层，属浏览器 IO 与
组件状态，在 `Game.Core.Tests` 中无法也不该测（遵循 UI→domain 单向依赖）。已有的
`SaveSystemTests` round-trip 测试已保证"导出串能被导入"这一核心契约。
