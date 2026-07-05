# Cookie Clicker — Blazor Fan Remake

> ⚠️ **Unofficial fan reimplementation.** Not affiliated with, endorsed by, or
> derived from the source code of Orteil / DashNet's Cookie Clicker. Built as
> a learning project for Blazor WebAssembly. See [`NOTICE.md`](./NOTICE.md)
> for full attribution and take-down policy.

用现代技术栈重构一个 UI 密集的老式增量游戏（`main.js` 一个 god class 装了 3000+ 行逻辑）。目标是把它拆成分层清晰、可测试、可持续演进的现代 Web 应用。原版仅作个人参考，不在本仓库内（见 [`NOTICE.md`](./NOTICE.md)）。

## 归属与范围（Attribution and scope）

- **原作** — Cookie Clicker 由 Julien "Orteil" Thiennot / DashNet 创作，
  官方免费网页版：<https://orteil.dashnet.org/cookieclicker/>，Steam 付费版
  见 store。原游戏的名称、美术、音效、剧情、文案版权归原作者所有。
- **本项目** — 用 C# / Blazor **从零重新实现**，未引用一行原版源码，未打
  包任何原版资源（图片/音频/图标）。所有 flavor text、成就名、升级名
  均为本项目原创。**保留的部分**：机制与数值（价格公式、CPS、tier 阈值、
  金饼干效果时长等，这些在多数司法辖区不受版权保护）以及 12 个建筑英文
  名（通用类别性词汇）。
- **本仓库许可** — `LICENSE`（MIT）只覆盖**代码**。游戏设计与商标不归本
  仓库所有，Fork 时请一并保留 `NOTICE.md`。
- **非商业** — 无广告、无监测、无收款渠道。
- **Take-down** — 原作者反对任何部分时立即下架，详见 `NOTICE.md`。

**建议阅读顺序**：如果你想 fork / redistribute，请先读 `NOTICE.md` 和 `LICENSE`。

---

## 技术栈：Blazor WebAssembly

**技术栈：**

| 层 | 选择 |
|---|---|
| 语言 | C# (.NET 10) |
| UI 框架 | Blazor WebAssembly |
| 逻辑层 | 独立的 `Game.Core` 类库（零 Blazor 依赖） |
| 状态管理 | C# 对象 + `StateHasChanged()` |
| 存档 | `System.Text.Json` + Base64 + `IJSRuntime` localStorage |
| 特效 | CSS + Blazor 组件（暂未引入 canvas） |
| 测试 | xUnit（针对 `Game.Core`，纯逻辑、本地秒级） |
| 构建 | `dotnet publish` → 纯静态文件 |
| 部署 | GitHub Pages（GitHub Actions 自动化） |

**选择理由：** 单一技术栈无胶水层、微软官方长期维护、类型系统天然逼着拆 god class、静态发布白嫖 Pages、`Game.Core` 可脱离浏览器 xUnit 测试。曾评估的其他方案（Godot / Unity / Uno / React+C#-WASM / 纯 React）见 [ADR 0001](docs/decisions/0001-technology-stack.md)。

**已知代价：**

- 首次加载多几 MB 的 .NET runtime（AOT + trimming 后可压到 1–2 MB）
- AI 生成 Blazor 代码的友好度低于 React（训练数据密度差一个数量级），需要更多人工校对

---

## 项目结构

```
cookie-clicker-remake/
├── Game.slnx                         .NET 10 XML solution
├── src/
│   ├── Game.Core/                    纯逻辑类库，零 Blazor 依赖
│   │   ├── Domain/
│   │   │   ├── BuildingId.cs / BuildingDefinition.cs
│   │   │   ├── UpgradeDefinition.cs
│   │   │   ├── AchievementDefinition.cs
│   │   │   ├── GoldenCookie.cs (含 ActiveBuff)
│   │   │   ├── OfflineEarningsSummary.cs
│   │   │   ├── ProgressionConfig.cs   糖块 / 转生 / 离线数值
│   │   │   └── GameState.cs          核心状态 + 操作
│   │   ├── Data/
│   │   │   ├── Buildings.cs          18 座建筑（含 6 座后期）
│   │   │   ├── Upgrades.cs           ~80 条升级
│   │   │   ├── Achievements.cs       ~90 条成就（含糖块 / 转生）
│   │   │   └── NewsFlavor.cs         新闻栏 flavor 文案池
│   │   ├── Formulas.cs               成本 / 批量公式
│   │   ├── NumberFormat.cs           "1.23 million" 展示
│   │   ├── SaveData.cs               版本化存档 DTO（v2）
│   │   └── SaveSystem.cs             JSON + Base64 + 步进 migrate
│   └── Game.Web/                     Blazor WASM
│       ├── Pages/Home.razor          三栏 shell + 粒子/浮字 + 离线弹窗
│       ├── Components/               BigCookie, StatsPanel, BuildingList,
│       │                             BuildingRow, UpgradeStore,
│       │                             AchievementList, GoldenCookieLayer,
│       │                             OptionsMenu, AchievementToasts,
│       │                             SugarLumpWidget, AscendPanel,
│       │                             NewsTicker, OfflineDialog, MuteButton
│       ├── Services/
│       │   ├── GameLoop.cs           30fps 定时器
│       │   ├── LocalStorageService.cs
│       │   ├── AudioService.cs       静音持久化 + JS interop
│       │   └── SaveCoordinator.cs    加载 / 自动保存 / 离线结算 / 导入 / 清档
│       ├── Layout/MainLayout.razor
│       ├── wwwroot/
│       │   └── js/cookie-clicker.js  Web Audio 合成音效
│       └── Program.cs
├── tests/
│   └── Game.Core.Tests/              xUnit（51 用例：公式/购买/升级/成就/存档/金饼干/糖块/转生/离线/迁移/新闻）
├── docs/
│   ├── README.md                     文档索引
│   ├── decisions/                    ADR
│   │   ├── 0001-technology-stack.md
│   │   ├── 0002-implementation.md
│   │   ├── 0003-late-game-and-polish.md
│   │   └── 0004-scope-boundary.md
│   └── retrospectives/               交付复盘
│       └── 0001-blazor-remake.md
├── .github/workflows/
│   └── deploy.yml                    Build → Test → Publish → Pages
├── LICENSE                           MIT（仅代码）
├── NOTICE.md                         归属、非商业、take-down 政策
├── .gitignore
└── README.md
```

---

## 本地开发

需求：**.NET 10 SDK**（本项目在 `10.0.301` 上验证通过）。

```bash
# 位于仓库根目录

# 首次或依赖变化时
dotnet restore Game.slnx

# 跑测试
dotnet test Game.slnx -c Release

# 本地开发（默认 https://localhost:5xxx，支持热重载）
dotnet watch --project src/Game.Web

# 静态发布（产物在 publish/wwwroot/）
dotnet publish src/Game.Web/Game.Web.csproj -c Release -o publish
```

> 首次 publish 会提示安装 `wasm-tools` workload 以获得更小的产物。可选：
> `dotnet workload install wasm-tools`

---

## 部署

`.github/workflows/deploy.yml` 会在 push 到 `main` 时自动：

1. `dotnet restore` → `dotnet test` → `dotnet publish`
2. 把 `<base href="/" />` 改写成 `<base href="/<repo-name>/" />`，适配 GitHub Pages 子路径
3. 生成 `.nojekyll`（避免 Jekyll 吞掉 `_framework/` 目录）
4. 复制 `index.html` 为 `404.html`（SPA fallback）
5. 通过 `actions/deploy-pages@v4` 部署

**首次启用需要**：在仓库 Settings → Pages 里把 Source 设为 "GitHub Actions"。

---

## 分层原则

1. **domain 层零 UI 依赖**：`Game.Core` 里只有纯逻辑，不 import 任何 Blazor / DOM API，可以脱离浏览器单元测试
2. **数据与逻辑分离**：建筑、升级、成就的定义放在 `Game.Core/Data/` 里的静态目录
3. **存档带版本号**：schema 版本化，写迁移函数，不允许出现"新版打不开老存档"
4. **UI 只负责渲染 + 输入**：不做业务判断，所有规则回到 domain 层

---

## 迁移 Roadmap（概要）

1. **M0 骨架**：搭好项目结构 + CI + Pages 部署 + 一个能点的 Big Cookie（+1 cookie）
2. **M1 建筑**：Cursor / Grandma / Farm ... 的购买、CPS 计算、tick loop
3. **M2 升级**：升级树 + 效果系统
4. **M3 成就**：判定系统 + 通知 UI
5. **M4 存档**：LocalStorage 持久化 + schema 迁移
6. **M5 特效**：粒子、掉落 cookie、飞起的 +1 文字
7. **M6 平衡与打磨**：对齐原版数值 / 音效 / 视觉

每个里程碑独立可玩、独立可测。

---

## 状态

- [x] 技术选型决策
- [x] M0 骨架搭建（Blazor solution + Game.Core + Game.Web + xUnit + CI）
- [x] M1 建筑系统（18 座建筑：早期 12 + 后期 6 · 单/十/百批量购买 · 成本递增公式）
- [x] M2 升级系统（每座建筑 4 档分级 + 4 档点击强化 + 3 档 Cursor 协同 + 4 档全局 CPS 加成）
- [x] M3 成就系统（330+ 条 · 累计烘焙 / 拥有数 / CPS / 点击 / 手工 / 金饼干 / 连击 / 升级 / 糖块 / 转生 / 游玩时长里程碑 · 分类 tab · 解锁弹窗）
- [x] M4 存档系统（版本化 JSON v3 · localStorage 15s 自动保存 · Base64 导入导出 · 步进迁移 · 一键清档）
- [x] 金饼干（Lucky / Frenzy / Click Frenzy · 13s 存续 · 60-300s 冷却）
- [x] **M5 后期系统 + 手感**（[ADR 0003](docs/decisions/0003-late-game-and-polish.md)）
  - 音效（Web Audio 合成，无外部素材）+ 静音持久化
  - 点击粒子（3-5 颗 emoji 碎屑）+ 浮字（`+N`）
  - 滚动新闻栏（事件优先 + ambient flavor + 进度型 headline）
  - 糖块系统（30 分钟成熟 · 每颗 +1% 永久 CPS）
  - 转生 / Prestige（`cbrt(baked/1e12)` · 每级 +2% 永久 CPS · 保留成就/糖块）
  - 离线收益（50% 效率 · 最多 24 小时 · 载入时"welcome back"弹窗）

---

## 决策日志

- **2026-07-04** 确定采用 Blazor WebAssembly。理由：本项目同时作为 Blazor 学习载体，技术栈统一优先于 AI 友好度。评估的其他方案（Godot / Unity / Uno / React+C#-WASM / 纯 React）见 [ADR 0001](docs/decisions/0001-technology-stack.md)。
- **2026-07-04** M0 骨架完成。目标框架 `net10.0`；用 .NET 10 新的 `Game.slnx` XML solution 格式；`Game.Core` 只有一个 `GameState.Click()`，3 个 xUnit 测试全部通过；`Game.Web` 提供一个可点的 Big Cookie 页面；`.github/workflows/deploy.yml` 配好 Pages 部署（含 base href 改写、`.nojekyll`、SPA 404 fallback）。
- **2026-07-04** M1–M4 + 金饼干全部完成，构成第一个可玩版本。`Game.Core` 领域层 ~600 行 C#，`Game.Web` UI 层 8 个组件 + 1 个 Home shell。**33/33 xUnit 测试通过**，`dotnet build Game.slnx -c Release` 0 警告 0 错误。实现思路和层级设计见 [ADR 0002](docs/decisions/0002-implementation.md)。
- **2026-07-04** 用户在 Rider F5 实机验证："完全能玩"，无阻断性 bug。对这次交付做了复盘，拆出"真正起作用的 5 个条件"与"没那么神的 5 处诚实清单"，并给出复现配方。详见 [复盘 0001](docs/retrospectives/0001-blazor-remake.md)。
- **2026-07-04** 为公开发布做仓库整理：清理逐字文案、加 NOTICE + LICENSE、把 Blazor 项目从 `blazor/` 子目录提升到根目录、移除原版参考代码副本、重置 git 历史。
- **2026-07-04** M5 完成，补齐 ADR 0002 里明确推迟的音效 / 粒子 / 新闻栏 / 后期建筑（Antimatter condenser → Idleverse）/ 糖块 / 转生 / 离线收益。存档 schema 升到 v2，51/51 xUnit 全绿，`dotnet build Game.slnx` 0 警告 0 错误。设计取舍见 [ADR 0003](docs/decisions/0003-late-game-and-polish.md)。
- **2026-07-04** 大幅扩充成就（98 → 334）：对齐原版建筑拥有 / 烘焙档位，新增 CPS / 手工烘焙 / 连击 / 游玩时长等纯增长轴成就族，存档 schema 升到 v3，加成就分类 tab。65/65 xUnit 全绿。
- **2026-07-04** 明确项目范围边界：**只做「纯指数增长玩具」形态**，主动拒绝 Garden / Stock Market / Pantheon / Grimoire / 季节活动等主动管理型后期系统，不以「成就数量追平原版」为目标。推理见 [ADR 0004](docs/decisions/0004-scope-boundary.md)。

> 完整的 ADR 索引、复盘索引和文档结构见 [`docs/README.md`](docs/README.md)。
