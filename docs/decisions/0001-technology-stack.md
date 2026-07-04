# ADR 0001：技术栈选型

- **状态**：已采纳（Plan A），保留 Plan B 作为切换预案
- **日期**：2026-07-04
- **决策者**：项目所有者

---

## 背景

原版 Cookie Clicker 是用 HTML + JS + CSS 写的老式浏览器游戏，核心逻辑集中
在一个 3000+ 行的 `main.js` god class 里，代码组织较差、技术较陈旧
（官方仍在 <https://orteil.dashnet.org/cookieclicker/> 免费提供浏览器版）。
目标是用现代技术栈**从零重新实现**，改善：

- **可维护性**：分层清晰、god class 拆分
- **可测试性**：核心逻辑能脱离浏览器单元测试
- **可持续演进**：便于加建筑、升级、成就、特效等新特性
- **AI 辅助开发友好度**：本项目主要靠 AI 辅助编码，技术栈的训练数据密度直接影响效率
- **零运维**：能白嫖 GitHub Pages 部署

同时有一个 **次要目标（但重要）**：本项目也作为熟悉 Blazor 的学习载体。

---

## 分析

### 第一层筛选：Cookie Clicker 是什么类型的游戏？

Cookie Clicker 的核心是：
- **一堆数字状态**（cookies、cps、建筑数量、升级列表、成就……）
- **UI 密集**（按钮、列表、tooltip、菜单、弹窗）
- **一个 tick loop**（每秒计算 CPS、自动保存）
- **少量特效**（掉落的 cookie、粒子、飞起的 +1 文字）

它 **不是** 需要物理、场景图、相机、精灵批渲染的游戏。所以用
Phaser / Unity / Godot / Cocos 这类"真·游戏引擎"是**杀鸡用牛刀**，
反而要跟它们的 Scene/Sprite 抽象作斗争，而 HTML/CSS 天然是做这种 UI 的最佳工具。

**结论：** 优先选"Web 应用 + 极少量 canvas 特效层"，而非"游戏引擎"。

---

### 候选方案总览

| # | 方案 | 语言 | UI | 备注 |
|---|---|---|---|---|
| A | **Blazor WebAssembly** | C# | Razor 组件 | 微软官方 Web 前端方案 |
| B | Godot 4 + C# | C# | Godot Control 节点 | 引擎路线，可导出 Web |
| C | Uno Platform / Avalonia | C# | XAML | WPF/WinUI 生态延伸 |
| D | **React + C# WASM** | C# + TS | React | 混合技术栈 |
| E | Unity + WebGL | C# | Unity UI | 引擎路线 |
| F | **纯 React + TS** | TypeScript | React | Web 原生 |
| — | Svelte / Vue / Phaser | — | — | Svelte 太小众；Phaser 对 UI 密集游戏不合适 |

---

### 逐个评估

#### ❌ 方案 B：Godot + C#

- 适合"顺便发到 Steam / 手机商店"的场景
- 对纯 Web + UI 密集的 Cookie Clicker 是杀鸡用牛刀
- Godot 的 Control 节点做大量列表 + tooltip 不如 HTML/CSS
- Web 导出包偏大
- **AI 训练数据密度低**，生成代码质量不稳
- **淘汰**

#### ❌ 方案 C：Uno / Avalonia

- 适合 WPF/WinUI 老手复用 XAML 技能
- 生态和文档远不如 Blazor，Web 导出更重
- **AI 训练数据密度低**
- **淘汰**

#### ❌ 方案 E：Unity + WebGL

- WebGL 首屏 10 MB+，加载慢
- IL2CPP 构建慢
- 对 DOM UI 主导的游戏是彻底的杀鸡用牛刀
- 2023 起 Unity 商业政策不友好
- **淘汰**

#### ⚠️ 方案 D：React + C# WASM

**架构：** 浏览器里同时跑 React（UI）和 C# WASM（逻辑），通过 `[JSExport]` / `[JSImport]` 交互。

**优点：**
- 产物是纯静态文件 → **能部署 GitHub Pages，无需服务器**
- React 部分 AI 友好度顶级
- C# 部分 AI 友好度顶级

**关键劣势（推翻了初步印象）：**
- **两个技术栈的边界**是最容易出错的地方
- `[JSExport]` / `[JSImport]` / `dotnet.js` runtime 用法**冷门中的冷门**
- AI 经常把它跟 Blazor 的 `IJSRuntime` 搞混（IJSRuntime 是 Blazor 场景专用的）
- 状态同步、序列化、生命周期都要自己设计——**没有官方最佳实践可抄**
- 每次跨边界都是一次 AI 出错的机会点

**综合 AI 友好度：反而不如方案 A**——纯 React 和纯 C# 都友好，但胶水层是黑洞。
- **淘汰（作为"想用 C#"的路线）**

#### ✅ 方案 A：Blazor WebAssembly

**架构：** C# 编译成 WASM，Razor 组件 + C# 代码后置，浏览器里跑。

**优点：**
- **单一技术栈**，一套心智模型，无胶水层
- 微软官方长期维护，文档完整
- 输出纯静态文件 → 白嫖 GitHub Pages
- C# 类型系统 + 独立类库结构，天然逼着拆分 god class
- 纯逻辑层可脱离浏览器 xUnit 测试，跑得比 Vitest 还快
- 是本项目"作为 Blazor 学习载体"这个次要目标的最优解

**劣势：**
- 首次加载多几 MB .NET runtime（AOT + trimming 后可压到 1–2 MB）
- **AI 生成代码友好度中等**：训练数据密度比 React 低一个数量级
- 高频状态更新时要注意 `StateHasChanged` 粒度

#### ✅ 方案 F：纯 React + TypeScript

**优点：**
- **AI 训练数据密度最高**，生成代码质量最稳
- 生态最完整、坑最少
- Web 原生技术栈，做 UI 密集游戏是最短路径
- TypeScript 严格模式 + Zod 足以约束 god class 不再出现
- 需要特效时随时能加 PixiJS 图层

**劣势（在本项目语境下）：**
- 无法满足"作为 Blazor 学习载体"这个次要目标

---

## 关键洞察

推翻了几个初步印象：

1. **"C# 后端 + React 前端 (方案 D)" 的 AI 友好度反而 < Blazor**。
   胶水层比整条 Blazor 链路的冷门程度更高。方案 D 是把两个技术栈"硬拼"，
   Blazor 是"官方原版"。

2. **"用 C# 类型系统才能干掉 god class" 这个论点站不住脚**。
   god class 的问题从来不是语言，而是有没有把逻辑拆出来。
   TypeScript + strict + Zod 完全能做到同等约束。

3. **"最简单的答案"就是纯 React**，为了用 C# 承担的都是净成本。
   这个成本值不值得，取决于**是否把"学 Blazor"当成项目目标之一**。

---

## 决策

**采纳方案 A：Blazor WebAssembly。**

**保留方案 F（纯 React + TS）作为切换预案。**

### 采纳 A 的关键理由

按重要性排序：

1. **本项目同时是 Blazor 的学习载体**——这条决定了技术栈统一 + 官方原版
   > AI 友好度差一点这个成本，学到 Blazor 的收益能覆盖
2. **技术栈单一**——避免方案 D 那种胶水层黑洞
3. **domain 类库 + xUnit** 是拆 god class 的干净起点
4. **静态发布 + Pages 部署** 满足零运维需求

### 主动接受的代价

- AI 生成 Blazor 代码需要更多人工校对（相比 React）
- 首屏加载几 MB（可通过 AOT + trimming + Brotli 缓解）
- Blazor 生态比 React 小，遇到冷门场景可能没现成方案

---

## 切换预案

**触发条件：** 如果 AI 辅助开发 Blazor 的效率明显低于预期——例如：

- AI 反复生成过时的 API（Blazor Server vs WASM 混淆、旧版 `@onclick` 语法等）
- 组件语法错误频繁到影响进度
- 状态更新/生命周期问题反复出现且 AI 难以自行修复

**切换动作：** 整体切换到方案 F（纯 React + TypeScript + Vite）。

**为切换降低成本的设计：**
- `Game.Core` 里的领域模型是纯逻辑、零 Blazor 依赖，切换时可以作为**规格参考**（不能直接搬，但公式和结构可翻译）
- xUnit 测试用例切换时可以逐条翻译成 Vitest，作为回归安全网

> 历史注记：M0–M4 交付期间曾在根目录预留了 `blazor/` 子目录、`.gitignore`
> 同时覆盖 .NET 和 Node，以便"两套并行"。M4 完成后确认不启动 Plan B，
> 仓库整理为单栈布局（.slnx 直接在根），若未来仍需切换预案，走"新起一个
> 仓库"或"以 orphan 分支重开"更干净，不再保留占位目录。

---

## 未来可能的补充方案

如果 Plan A 走下来觉得需要 canvas 级别的粒子特效，可以在 `Game.Web` 里通过
`IJSRuntime` 引一层 PixiJS 或直接用 `<canvas>` + Canvas2D，这不改变主线技术栈选择。

---

## 参考

- 项目 README：`../../README.md`
- 原版官方浏览器版：<https://orteil.dashnet.org/cookieclicker/>
- 归属与非商业政策：`../../NOTICE.md`
