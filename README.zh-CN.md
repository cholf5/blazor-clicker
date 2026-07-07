<div align="center">

# 🍪 Cookie Clicker — Blazor 粉丝复刻版

**一个非官方、从零重写的 Cookie Clicker 早期成长循环，使用 C# / Blazor WebAssembly 构建。**

[![立即游玩](https://img.shields.io/badge/▶_立即游玩-GitHub_Pages-2ea44f?style=for-the-badge)](https://cholf5.github.io/blazor-clicker/)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor WASM](https://img.shields.io/badge/Blazor-WebAssembly-512BD4?style=flat-square&logo=blazor)](https://learn.microsoft.com/aspnet/core/blazor/)
[![测试](https://img.shields.io/badge/tests-99_passing-2ea44f?style=flat-square)](tests/Game.Core.Tests)
[![许可证: MIT](https://img.shields.io/badge/License-MIT_(code)-blue?style=flat-square)](LICENSE)
[![多语言](https://img.shields.io/badge/i18n-EN_·_简中_·_繁中-orange?style=flat-square)](src/Game.Core/Localization)

<img src="docs/screenshots/demo.gif" alt="玩法演示" width="800">

**English version: [`README.md`](./README.md)**

</div>

> ⚠️ **非官方粉丝作品。** 未附属于、未获授权于、也未复制原版 Cookie Clicker（Orteil /
> DashNet 出品）的源码；未复用任何原版源码、美术、音效或文案，仅保留不受版权保护的
> 机制/数值与 12 个通用建筑名。完整归属与下架政策详见 [`NOTICE.md`](./NOTICE.md)。

---

## ✨ 这是什么？

用现代技术栈**重构**一个 UI 密集的老式增量游戏——原版的 `main.js` 把 3000+ 行逻辑
塞进单个 god class。目标是把早期的「纯指数增长」玩具重建成一个**分层清晰、可测试、
可维护**的 Web 应用，并把它当作一次认真的 Blazor WebAssembly 学习实践。

成果是一个**完整、自包含的核心游戏**，打开浏览器即玩——无需安装、无账号、无广告、无监测。

<div align="center">
<table>
<tr>
<td width="50%"><img src="docs/screenshots/gameplay.png" alt="中期玩法" width="100%"></td>
<td width="50%"><img src="docs/screenshots/options.png" alt="选项界面" width="100%"></td>
</tr>
<tr>
<td align="center"><em>三栏布局 · 建筑 / CPS / 成就</em></td>
<td align="center"><em>选项 · 支持多种语言</em></td>
</tr>
</table>
</div>

---

## 🎮 功能亮点

- 🏭 **18 座建筑** —— 早期 12 座（光标 → 时光机）外加 6 座后期建筑，支持单个 / ×10 / ×100
  批量购买与成本递增公式。
- 🔬 **83 项升级** —— 各建筑分级倍率、点击威力、光标协同以及全局 CPS 加成，全部由通用的
  `EffectKind` 系统驱动。
- 🏆 **334 项成就** —— 烘焙量/CPS 里程碑阶梯、点击、金饼干连击、糖块、飞升与游玩时长，
  分组为可筛选的标签页。
- 🍀 **金饼干** —— 幸运 / 狂热 / 点击狂热增益，定时刷新。
- 🥛 **牛奶** —— 由成就驱动的被动 CPS 轴。
- 🍬 **糖块** —— 可消耗的建筑等级货币（24 小时成熟）。
- ⭐ **威望 / 飞升** —— `cbrt(baked / 1e12)` 天堂芯片，附带永久 CPS 加成。
- 💤 **离线收益** —— 50% 效率，上限 24 小时，附「欢迎回来」弹窗。
- 🔊 **合成音效** —— Web Audio，零外部素材；粒子与浮动文字特效。
- 📰 **新闻滚动条** —— 事件优先级 + 环境风味 + 进度头条。
- 🌍 **三语支持** —— 英文、简体中文、繁體中文（英文为源语言与回退语言）。
- 💾 **版本化存档** —— JSON + Base64 导出/导入，15 秒自动保存，逐级迁移。

---

## 🛠 技术栈

| 层 | 选择 |
|---|---|
| 语言 | C# (.NET 10) |
| UI | Blazor WebAssembly |
| 领域逻辑 | 独立的 `Game.Core` 类库 —— **零** Blazor/DOM/JS 依赖 |
| 状态 | 纯 C# 对象 + `StateHasChanged()` 轮询 |
| 持久化 | `System.Text.Json` + Base64 + `IJSRuntime` localStorage |
| 特效 | CSS + Blazor 组件 + Web Audio（无 canvas） |
| 测试 | 针对 `Game.Core` 的 xUnit —— 纯逻辑、亚秒级、无需浏览器 |
| 构建与部署 | `dotnet publish` → 静态文件 → GitHub Pages（GitHub Actions） |

**为什么选 Blazor？** 单一技术栈、无胶水层、微软官方长期维护，类型系统天然逼着拆 god class；
`Game.Core` 可脱离浏览器用 xUnit 测试，静态发布白嫖 GitHub Pages。其他备选方案（Godot /
Unity / Uno / React+C#-WASM / 纯 React）的取舍见 [ADR 0001](docs/decisions/0001-technology-stack.md)。

**已知代价：** 首次加载会拉取几 MB 的 .NET 运行时（AOT + 裁剪可压到 1–2 MB）；Blazor 的
AI 代码生成成熟度不如 React。

---

## 🏗 架构

严格的单向依赖：**UI → 领域，绝不反向。**

```
src/
├── Game.Core/          # 整个游戏，纯 C#。无 Blazor/DOM/JS。所有规则都在这里。
│   ├── Domain/         #   GameState（聚合根）、建筑、升级、金饼干……
│   ├── Data/           #   静态目录：18 建筑、83 升级、334 成就、新闻
│   ├── Localization/   #   EN / 简中 / 繁中 覆盖字典 + 检测
│   ├── Formulas.cs     #   成本与批量定价数学（经济常量集中管理）
│   └── SaveSystem.cs   #   JSON ⇄ SaveData DTO (v5) + Base64 + 逐级迁移
├── Game.Web/           # Blazor WASM。渲染 Game.Core、转发输入。无业务逻辑。
│   ├── Components/      #   BigCookie、StatsPanel、UpgradeStore、AchievementList、tooltip……
│   ├── Services/        #   GameLoop（~30fps）、SaveCoordinator、Audio、Tooltip、UpdateChecker
│   └── Pages/Home.razor #   三栏外壳 + 粒子 + 离线弹窗
└── tests/Game.Core.Tests/   # 99 个 xUnit 用例 —— 公式、购买、升级、成就、
                              # 存档、金饼干、糖块、威望、离线、i18n
```

关键不变量：

- **`Tick(deltaSeconds)` 是唯一的时钟。** 它按顺序应用被动 CPS、过期增益、刷新/消失金饼干、
  糖块成熟、检查成就。
- **UI 轮询**查询方法（`CurrentCps()`、`ClickPower()`、`AvailableUpgrades()`……）
  每帧执行；不缓存。通知单向流动：领域入队，UI 排空。
- **内容即数据，非代码。** 要加建筑/升级/成就，就往目录里加一个 `record` —— 别把条件判断
  散落进 `GameState`。
- **存档是版本化的。** 任何结构变更都要提升 `CurrentVersion` 并加一步迁移。

完整 ADR 索引、复盘与文档结构见 [`docs/README.md`](docs/README.md)。

---

## 🚀 本地开发

需要 **.NET 10 SDK**（在 `10.0.301` 上验证通过）。

```bash
# 还原（首次运行 / 依赖变更时）
dotnet restore Game.slnx

# 运行全部测试
dotnet test Game.slnx -c Release

# 带热重载的本地开发服务器 → https://localhost:5xxx
dotnet watch --project src/Game.Web

# 静态发布（输出在 publish/wwwroot/）
dotnet publish src/Game.Web/Game.Web.csproj -c Release -o publish
```

> 首次发布可能提示你安装 `wasm-tools` workload 以获得更小的产物：
> `dotnet workload install wasm-tools`

---

## 📦 部署

推送到 `main` 会触发 [`deploy.yml`](.github/workflows/deploy.yml)：**还原 → 测试 →
发布 → GitHub Pages**。工作流会为仓库子路径重写 `<base href>`，添加 `.nojekyll`，
并把 `index.html` 复制为 `404.html` 用于 SPA 回退。

在 fork 上启用：**Settings → Pages → Source = "GitHub Actions"。**

---

## 📐 范围边界

依据 [ADR 0004](docs/decisions/0004-scope-boundary.md)，本项目**主动**只实现早期纯成长
玩具，并**拒绝**主动管理型的后期系统：园艺、股市、万神殿、魔法书、季节活动，以及（默认）
虫群 / 奶奶启示录。成就数量（~334 对比原版 622）是刻意**不去**追赶的——差距是不做那些子系统
的结果，而非待办。新成就应沿纯成长轴扩展，而非为了凑数。

这不是「半成品」，而是**主动收敛的完整核心**：只做纯指数增长玩具，拒绝园艺/股市/万神殿等
主动管理型后期系统。成就数量差距是刻意取舍，不是待办。

---

## 📄 许可与归属

- **原版游戏** —— Cookie Clicker，作者 Julien "Orteil" Thiennot / DashNet
  （[官方免费网页版](https://orteil.dashnet.org/cookieclicker/)）。名称、美术、音效、
  剧情与文案版权归原作者所有。
- **本仓库的 [`LICENSE`](LICENSE)（MIT）仅覆盖代码。** 游戏设计与商标不归我们授权 ——
  fork 时请保留 [`NOTICE.md`](./NOTICE.md)。
- **非商业** —— 无广告、无监测、无支付渠道。
- **下架** —— 若原作者反对将立即移除（见 `NOTICE.md`）。

**在 fork 或再分发前，请先阅读 [`NOTICE.md`](./NOTICE.md) 与 [`LICENSE`](LICENSE)。**

---

<div align="center">
<sub>作为 Blazor WebAssembly 学习项目构建。如果你喜欢它，欢迎点个 ⭐。</sub>
</div>
