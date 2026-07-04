# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

An unofficial fan reimplementation of Cookie Clicker's *early-game* pure-growth
loop, built in C# / Blazor WebAssembly as a learning project. See `NOTICE.md`
for the attribution and take-down policy — no original source, art, audio, or
flavor text is reused; only unprotectable mechanics/numbers and 12 generic
building names are kept.

Requires **.NET 10 SDK** (verified on `10.0.301`).

## Commands

```bash
# Restore (first run or when dependencies change)
dotnet restore Game.slnx

# Run all tests
dotnet test Game.slnx -c Release

# Run a single test by name filter
dotnet test Game.slnx --filter "FullyQualifiedName~SaveSystemTests"
dotnet test Game.slnx --filter "DisplayName~Migrate"

# Local dev server with hot reload (https://localhost:5xxx)
dotnet watch --project src/Game.Web

# Build check (expected: 0 warnings, 0 errors)
dotnet build Game.slnx -c Release

# Static publish (output in publish/wwwroot/)
dotnet publish src/Game.Web/Game.Web.csproj -c Release -o publish
```

> **Iron rule — never background `dotnet test`/`build`/`watch`.** Always run
> them in the foreground and wait for the exit. Backgrounding a test run and
> then polling with `sleep` reads as "stuck", and a stray `testhost` keeps the
> output DLL locked so the next build fails with `MSB3021 (file in use)`. If a
> run ever leaves a lock behind, clear it with
> `Get-Process testhost,dotnet | Where-Object Id -ne $PID | Stop-Process -Force`
> before rebuilding. To go faster, narrow the run with `--filter`, don't
> background it.

Pushes to `main` trigger `.github/workflows/deploy.yml` (restore → test →
publish → GitHub Pages), which rewrites `<base href>` for the repo subpath,
adds `.nojekyll`, and copies `index.html` to `404.html` for SPA fallback.

## Architecture

Two projects plus tests, with a strict one-way dependency: **UI → domain, never the reverse.**

- **`src/Game.Core`** — the entire game as pure C#. Zero Blazor/DOM/JS
  dependencies, so it runs under xUnit without a browser. This is where *all*
  game rules live.
- **`src/Game.Web`** — Blazor WASM. Renders `Game.Core` state and forwards
  input. Does **no** business logic — every rule decision routes back into the
  domain layer.
- **`tests/Game.Core.Tests`** — xUnit against `Game.Core` only.

### The domain core

`GameState` (`Game.Core/Domain/GameState.cs`) is the aggregate root — all
mutable state and the operations that transition it. Key invariants:

- **`Tick(deltaSeconds)` is the only thing that advances the simulation clock.**
  It applies passive CPS, expires buffs, spawns/despawns golden cookies, ripens
  sugar lumps, and checks achievements — in that order.
- The UI **polls** `GameState` query methods (`CurrentCps()`, `ClickPower()`,
  `NextBuildingCost()`, `AvailableUpgrades()`, …) every frame. These are
  computed on demand from state, not cached.
- Notifications flow one way: `GameState` enqueues achievement IDs and news
  strings internally; the UI **drains** them each frame via
  `DrainAchievementNotifications()` / `DrainNewsMessages()`.
- CPS composition (see `CurrentCpsRaw`): per-building base × building-tier
  multipliers, plus cursor synergy (flat CPS per non-cursor building), then a
  multiplicative global multiplier, then a permanent bonus from prestige +
  sugar lumps. Temporary Frenzy buffs multiply on top in `CurrentCps()`.

### Data-driven catalogs

Buildings, upgrades, and achievements are **static definition catalogs** in
`Game.Core/Data/` (`Buildings.cs`, `Upgrades.cs`, `Achievements.cs`), not
hard-coded logic. Definitions are `record`-like objects; unlock conditions are
`Func<GameState, bool>` predicates evaluated against state.

**To add content, add a definition to the catalog** — do not scatter conditionals
through `GameState`. Upgrades declare an `EffectKind` (e.g.
`BuildingMultiplier`, `ClickMultiplier`, `GlobalCpsMultiplier`,
`CursorPerNonCursorBuilding`) that `GameState`'s CPS/click math already
interprets generically. Building tier upgrades are generated in a loop from a
`Tiers` table rather than written out one by one.

### Saves are versioned — migrations are mandatory

`SaveSystem` (`Game.Core/SaveSystem.cs`) serializes `GameState` ⇄ `SaveData`
DTO as JSON (exports are additionally Base64-wrapped for safe sharing).
`SaveData.Version` is currently **3**.

- **Any change to the persisted shape must bump `CurrentVersion` and add a
  step migration case** in `Migrate()`. The pattern is stepwise
  (`v1→v2→v3→…`), each case backfilling only what's new. Loading a newer-than-
  known save throws rather than silently corrupting.
- New counters that default to 0/false for old saves need no backfill — just
  the version bump and an empty case documenting why.

### Web layer wiring

`Program.cs` registers the service graph. `SaveCoordinator` (singleton) **owns
the current `GameState`**, loads it on startup, applies offline earnings from
the wall-clock gap since last save, and autosaves every 15s. `GameLoop`
(singleton) ticks ~30fps, reads `SaveCoordinator.State` fresh each tick (so
import/wipe take effect immediately), calls `Tick`, and raises `OnTick` for
re-render. State replacement (import/wipe) fires `OnStateReplaced`.

`Home.razor` is the three-column shell; components under
`Game.Web/Components/` each take `State` as a parameter and raise callbacks for
input.

## Scope boundary (read before adding features)

Per [ADR 0004](docs/decisions/0004-scope-boundary.md), this project
**deliberately** implements only the early-game "pure exponential growth" toy
and **rejects** active-management late-game systems: Garden, Stock Market,
Pantheon, Grimoire, seasonal events, and (by default) Wrinklers /
Grandmapocalypse. Achievement count (~334) is intentionally *not* chased toward
the original's 622 — that gap is a consequence of not building those systems,
not a backlog.

**Correct way to add achievements:** extend the pure-growth axis (finer CPS/baked
milestone tiers, single-run constraint achievements, combination conditions) —
one `record` plus at most one new counter. Do **not** add a new subsystem to
pad the count. Overturning this direction requires a new ADR, not a quiet
feature addition.

## Conventions

- Domain code carries `<summary>` doc comments explaining *why*, especially on
  economy math (`Formulas.cs`, `CurrentCpsRaw`) and save migrations. Match this
  density.
- Keep economic constants in one place (`Formulas.cs`, `ProgressionConfig.cs`).
- Every milestone should stay independently playable and testable; new domain
  behavior gets an xUnit test in `Game.Core.Tests`.
- Architecture decisions live in `docs/decisions/` (ADRs) — consult them, and
  add one for consequential choices.
