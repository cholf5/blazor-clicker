# WASM 版本更新提示机制 — 设计

日期：2026-07-06
状态：已验证，待实施

## 背景与问题

本项目是部署到 GitHub Pages 的标准 Blazor WASM 静态站，**没有 Service
Worker、没有 PWA**。`index.html` 通过 .NET fingerprint 机制
（`blazor.webassembly#[.{fingerprint}].js`）引用 `_framework/` 资源。

发布新版本后玩家能否更新，取决于浏览器是否重新拉取 `index.html`：

- `_framework/` 下的 DLL/wasm/boot.json 都是内容指纹命名，新版必变文件名，
  天然可缓存、天然会更新——不是问题。
- 真正的扳机是 `index.html`。GitHub Pages 对 HTML 默认返回
  `Cache-Control: max-age=600`（10 分钟）+ ETag，**不是永不过期**。
- **痛点**：玩家若一直挂着标签页、从不刷新，SPA 加载完成后浏览器根本不会
  再请求 `index.html`，页面会**永远停在旧版**——10 分钟 HTML 缓存对「已在
  运行的页面」毫无作用。

## 目标

解决「玩家长时间挂机不刷新，永远停在旧版」的场景：应用内轮询检测线上版本，
发现新版时以**不打断游戏**的方式提示玩家，由玩家自己决定何时刷新。

## 非目标（YAGNI）

- 不做离线可玩 / 秒开——那是 PWA 的卖点，非本项目诉求。
- 不做 Service Worker——其「先用缓存、后台更新、下次生效」的默认策略反而会
  让玩家卡在旧版更久，与目标背道而驰，且子路径部署 + `<base href>` 重写下
  scope/缓存清单坑多。
- 不做「稍后再提醒」等复杂调度。关闭横条 = 本次隐藏，玩家下次自行刷新即更新。

## 方案选型记录

| 方案 | 结论 |
|------|------|
| 1. 仅靠浏览器缓存过期 | 现状，无法覆盖「挂机不刷新」场景 |
| **2. 应用内版本轮询 + 提示条** | **采用**：最小复杂度精准命中诉求，刷新时机交给玩家 |
| 3. PWA / Service Worker | 否决：核心卖点（离线/秒开）非本项目诉求，反而引入更新滞后与调试成本 |

版本指纹来源：采用 **CI 生成极简 `version.json`**（体积最小、语义最清晰），
标识用 **git commit SHA**（唯一、可追溯；优于会误报的时间戳或易忘的手工版本号）。

## 设计

### 第 1 节 · 版本产物

在 `.github/workflows/deploy.yml` 中新增一步，生成
`publish/wwwroot/version.json`，用 7 位 commit SHA 作版本标识：

```yaml
- name: Write version.json
  run: echo "{\"version\":\"${GITHUB_SHA::7}\"}" > publish/wwwroot/version.json
```

- 位置：在 `Upload Pages artifact` 之前即可。
- 本地开发：在 `src/Game.Web/wwwroot/` 放占位文件 `version.json`
  （`{"version":"dev"}`），避免 `dotnet watch` 时控制台 404 噪音；CI 发布时
  被覆盖。

### 第 2 节 · UpdateChecker 服务

新增 `src/Game.Web/Services/UpdateChecker.cs`，singleton，模式对齐现有
`GameLoop`（`System.Timers.Timer` + 事件），纯 Web 层，不碰 domain。

```csharp
public sealed class UpdateChecker : IDisposable
{
    private const double PollIntervalSeconds = 600; // 10 分钟

    private readonly HttpClient _http;
    private readonly System.Timers.Timer _timer;
    private string? _baseline;
    public bool UpdateAvailable { get; private set; }
    public event Action? OnUpdateAvailable;

    public async Task StartAsync()
    {
        _baseline = await FetchVersionAsync(); // 记基线；拿不到就 null
        _timer.Start();
    }

    private async Task<string?> FetchVersionAsync()
    {
        try
        {
            // 关键：绕过 HTTP 缓存，否则永远读到旧值
            var url = $"version.json?_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var doc = await _http.GetFromJsonAsync<VersionDto>(url);
            return doc?.Version;
        }
        catch { return null; } // 404 / 离线 / 解析失败 → 静默
    }
}
```

行为约定：

- **基线拿不到就躺平**：`_baseline == null`（本地 dev、首次网络抖动）时，后续
  即使拿到值也不报更新——没有可信基线不打扰玩家。
- **只报一次**：轮询发现版本 != 基线 → 置 `UpdateAvailable = true`、停 timer、
  `OnUpdateAvailable?.Invoke()`，不重复弹。
- **防缓存**靠查询参数 `?_=<毫秒时间戳>`——本方案最易踩空的点。
- 单次轮询失败不改状态，下周期再试。

注册：`Program.cs` 加 `builder.Services.AddSingleton<UpdateChecker>()`；
`Home.razor` 的 `OnInitializedAsync` 中 `Loop.Start()` 之后
`await Updater.StartAsync()`。

### 第 3 节 · 更新提示条 UI

新增 `src/Game.Web/Components/UpdateBanner.razor`，挂在 `Home.razor` 的
masthead **正上方**、做成与 NewsTicker 同一视觉家族的「置顶公告」（玩家习惯在
顶部看新闻，「有新版」也算一条新闻；但滚动新闻承载不了持久可点的 CTA，故用
独立稳定横条）。

```razor
<UpdateBanner />          <!-- 有新版才出现，全宽，置顶公告样式 -->
<div class="game-masthead">
    <NewsTicker ... />
    <OptionsMenu ... />
</div>
```

文案（全部走 `ILocalizer`，中英各一条）：

```
🍪 有新版本可用  [ 立即刷新 ]  [ ✕ ]
```

- key：`ui.update.available`、`ui.update.refresh`、`ui.update.dismiss`。
- 平时不渲染；`UpdateChecker.UpdateAvailable == true` 才出现。
- `OnInitialized` 订阅 `Updater.OnUpdateAvailable`，回调
  `InvokeAsync(StateHasChanged)`；`IDisposable` 退订（对齐现有组件写法）。
- **「立即刷新」**：先 `await Save.SaveNowAsync()` 主动存档（15s autosave 之上
  的安全网，确保一颗饼干都不丢），再 `IJSRuntime.InvokeVoidAsync("location.reload")`
  （零新增 JS）。
- **「✕」**：仅本次隐藏横条（timer 已停、不会再弹）。

## 数据流

```
CI 发布 → version.json(SHA)
  ↓
UpdateChecker.StartAsync() 记基线
  ↓ 每 10 分钟（带时间戳绕缓存）
FetchVersionAsync() 比对
  ↓ 版本变化
UpdateAvailable=true → OnUpdateAvailable
  ↓
UpdateBanner 显示 → 玩家点「立即刷新」
  ↓
SaveNowAsync() → location.reload() → 拿到新 index.html → 全量更新
```

## 测试

- `Game.Web.Tests`：`UpdateChecker` 版本比对逻辑——基线为 null 时不报更新、
  版本相同不报、版本不同报一次且停止再报。HttpClient 用可注入的 fake handler
  喂不同 version.json 响应。
- 边界：轮询抛异常时状态不变。

## 变更清单

- `.github/workflows/deploy.yml` — 新增 Write version.json 步骤
- `src/Game.Web/wwwroot/version.json` — 占位文件（新增）
- `src/Game.Web/Services/UpdateChecker.cs` — 新增
- `src/Game.Web/Components/UpdateBanner.razor` — 新增
- `src/Game.Web/Program.cs` — 注册 UpdateChecker
- `src/Game.Web/Pages/Home.razor` — 启动 checker + 挂 UpdateBanner
- 本地化资源 — 3 个 ui.update.* key（中英）
- `tests/Game.Web.Tests/` — UpdateChecker 测试
