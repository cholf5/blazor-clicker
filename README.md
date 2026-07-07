<div align="center">

# 🍪 Cookie Clicker — Blazor Fan Remake

**An unofficial, from-scratch reimplementation of Cookie Clicker's early-game growth loop, built in C# / Blazor WebAssembly.**

[![Play now](https://img.shields.io/badge/▶_Play_now-GitHub_Pages-2ea44f?style=for-the-badge)](https://cholf5.github.io/blazor-clicker/)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![Blazor WASM](https://img.shields.io/badge/Blazor-WebAssembly-512BD4?style=flat-square&logo=blazor)](https://learn.microsoft.com/aspnet/core/blazor/)
[![Tests](https://img.shields.io/badge/tests-99_passing-2ea44f?style=flat-square)](tests/Game.Core.Tests)
[![License: MIT](https://img.shields.io/badge/License-MIT_(code)-blue?style=flat-square)](LICENSE)
[![i18n](https://img.shields.io/badge/i18n-EN_·_简中_·_繁中-orange?style=flat-square)](src/Game.Core/Localization)

<img src="docs/screenshots/demo.gif" alt="Gameplay demo" width="800">

**中文版本: [`README.zh-CN.md`](./README.zh-CN.md)**

</div>

> ⚠️ **Unofficial fan project.** Not affiliated with, endorsed by, or derived from the
> source code of Orteil / DashNet's Cookie Clicker. No original source, art, audio, or
> flavor text is reused — only unprotectable mechanics/numbers and 12 generic building
> names. See [`NOTICE.md`](./NOTICE.md) for full attribution and the take-down policy.

---

## ✨ What is this?

A **modern rebuild** of an old, UI-dense incremental game — the kind whose original
`main.js` crams 3000+ lines into a single god class. The goal was to reconstruct the
early-game *pure exponential growth* toy as a **layered, testable, maintainable** web
app, and to use it as a serious Blazor WebAssembly learning vehicle.

The result is a **complete, self-contained core game** you can play right now in your
browser — no install, no account, no ads, no tracking.

<div align="center">
<table>
<tr>
<td width="50%"><img src="docs/screenshots/gameplay.png" alt="Mid-game" width="100%"></td>
<td width="50%"><img src="docs/screenshots/options.png" alt="Late-game" width="100%"></td>
</tr>
<tr>
<td align="center"><em>Three-column layout · buildings / CPS / achievements</em></td>
<td align="center"><em>Options · multi-language support</em></td>
</tr>
</table>
</div>

---

## 🎮 Features

- 🏭 **18 buildings** — the early 12 (Cursor → Time Machine) plus 6 late-game
  additions, with single / ×10 / ×100 batch buying and cost-scaling formulas.
- 🔬 **83 upgrades** — per-building tier multipliers, click power, cursor synergy,
  and global CPS boosts, all driven by a generic `EffectKind` system.
- 🏆 **334 achievements** — baked/CPS milestone ladders, clicking, golden-cookie
  combos, sugar lumps, ascensions and play-time, grouped into filterable tabs.
- 🍀 **Golden cookies** — Lucky / Frenzy / Click Frenzy buffs, timed spawns.
- 🥛 **Milk** — a passive, achievement-driven CPS axis.
- 🍬 **Sugar lumps** — a spendable building-level currency (24h ripening).
- ⭐ **Prestige / Ascension** — `cbrt(baked / 1e12)` heavenly chips, +permanent CPS.
- 💤 **Offline earnings** — 50% efficiency, capped at 24h, "welcome back" dialog.
- 🔊 **Synthesized audio** — Web Audio, zero external assets; particle & floating-text FX.
- 📰 **News ticker** — event-priority + ambient flavor + progress headlines.
- 🌍 **Trilingual** — English, 简体中文, 繁體中文 (English is the source & fallback).
- 💾 **Versioned saves** — JSON + Base64 export/import, 15s autosave, stepwise migration.

---

## 🛠 Tech Stack

| Layer | Choice |
|---|---|
| Language | C# (.NET 10) |
| UI | Blazor WebAssembly |
| Domain logic | Standalone `Game.Core` class library — **zero** Blazor/DOM/JS deps |
| State | Plain C# objects + `StateHasChanged()` polling |
| Persistence | `System.Text.Json` + Base64 + `IJSRuntime` localStorage |
| Effects | CSS + Blazor components + Web Audio (no canvas) |
| Tests | xUnit against `Game.Core` — pure logic, sub-second, browser-free |
| Build & deploy | `dotnet publish` → static files → GitHub Pages (GitHub Actions) |

**Why Blazor?** One language, no glue layer, first-party long-term support, and a type
system that *forces* the god class apart. `Game.Core` runs headless under xUnit, and the
static publish deploys free to GitHub Pages. Alternatives evaluated (Godot / Unity / Uno /
React+C#-WASM / plain React) are recorded in [ADR 0001](docs/decisions/0001-technology-stack.md).

**Known trade-offs:** the first load pulls a few MB of .NET runtime (AOT +
trimming can shrink it to 1–2 MB); AI codegen for Blazor is less mature than for React.

---

## 🏗 Architecture

Strict one-way dependency: **UI → domain, never the reverse.**

```
src/
├── Game.Core/          # The whole game as pure C#. No Blazor/DOM/JS. All rules live here.
│   ├── Domain/         #   GameState (aggregate root), buildings, upgrades, golden cookies…
│   ├── Data/           #   Static catalogs: 18 buildings, 83 upgrades, 334 achievements, news
│   ├── Localization/   #   EN / 简中 / 繁中 overlay dictionaries + detection
│   ├── Formulas.cs     #   Cost & batch-price math (economy constants centralized)
│   └── SaveSystem.cs   #   JSON ⇄ SaveData DTO (v5) + Base64 + stepwise migration
├── Game.Web/           # Blazor WASM. Renders Game.Core, forwards input. No business logic.
│   ├── Components/      #   BigCookie, StatsPanel, UpgradeStore, AchievementList, tooltips…
│   ├── Services/        #   GameLoop (~30fps), SaveCoordinator, Audio, Tooltip, UpdateChecker
│   └── Pages/Home.razor #   Three-column shell + particles + offline dialog
└── tests/Game.Core.Tests/   # 99 xUnit cases — formulas, buying, upgrades, achievements,
                              # saves, golden cookies, sugar lumps, prestige, offline, i18n
```

Key invariants:

- **`Tick(deltaSeconds)` is the only clock.** It applies passive CPS, expires buffs,
  spawns/despawns golden cookies, ripens sugar lumps, and checks achievements — in order.
- **The UI polls** query methods (`CurrentCps()`, `ClickPower()`, `AvailableUpgrades()`…)
  every frame; nothing is cached. Notifications flow one way: the domain enqueues, the UI drains.
- **Content is data, not code.** To add a building/upgrade/achievement, add a `record` to a
  catalog — don't scatter conditionals through `GameState`.
- **Saves are versioned.** Any schema change bumps `CurrentVersion` and adds a step migration.

Full ADR index, retrospectives and doc structure: [`docs/README.md`](docs/README.md).

---

## 🚀 Getting Started

Requires **.NET 10 SDK** (verified on `10.0.301`).

```bash
# Restore (first run / when dependencies change)
dotnet restore Game.slnx

# Run all tests
dotnet test Game.slnx -c Release

# Local dev server with hot reload → https://localhost:5xxx
dotnet watch --project src/Game.Web

# Static publish (output in publish/wwwroot/)
dotnet publish src/Game.Web/Game.Web.csproj -c Release -o publish
```

> First publish may prompt you to install the `wasm-tools` workload for smaller output:
> `dotnet workload install wasm-tools`

---

## 📦 Deployment

Pushes to `main` trigger [`deploy.yml`](.github/workflows/deploy.yml): **restore → test →
publish → GitHub Pages**. The workflow rewrites `<base href>` for the repo subpath, adds
`.nojekyll`, and copies `index.html` to `404.html` for SPA fallback.

To enable on a fork: **Settings → Pages → Source = "GitHub Actions".**

---

## 📐 Scope Boundary

Per [ADR 0004](docs/decisions/0004-scope-boundary.md), this project **deliberately**
implements only the early-game pure-growth toy and **rejects** active-management late-game
systems: Garden, Stock Market, Pantheon, Grimoire, seasonal events, and (by default)
Wrinklers / Grandmapocalypse. The achievement count (~334 vs. the original's 622) is
intentionally *not* chased — the gap is a consequence of not building those subsystems, not
a backlog. New achievements should extend the pure-growth axis, not pad the count.

---

## 📄 License & Attribution

- **Original game** — Cookie Clicker by Julien "Orteil" Thiennot / DashNet
  ([official free web version](https://orteil.dashnet.org/cookieclicker/)). Names, art,
  audio, story and flavor text are © the original author.
- **This repo's [`LICENSE`](LICENSE) (MIT) covers the code only.** Game design and
  trademarks are not ours to license — keep [`NOTICE.md`](./NOTICE.md) when forking.
- **Non-commercial** — no ads, no tracking, no payment channels.
- **Take-down** — removed immediately if the original author objects (see `NOTICE.md`).

**Before you fork or redistribute, read [`NOTICE.md`](./NOTICE.md) and [`LICENSE`](LICENSE) first.**

---

<div align="center">
<sub>Built as a Blazor WebAssembly learning project. If you like it, consider leaving a ⭐.</sub>
</div>
