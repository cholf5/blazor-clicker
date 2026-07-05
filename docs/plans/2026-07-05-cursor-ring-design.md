# 光标环（Cursor Ring）设计

**日期**：2026-07-05
**状态**：已确认，待实施
**范围**：Web 层纯展示特性，零 domain 改动、零存档影响

## 背景与目标

原版 Cookie Clicker 在大饼干周围有一圈随 Cursor（光标建筑）数量增长、绕饼干
旋转并周期性戳击饼干的手指/光标，作为"自动点击"的视觉隐喻。本 remake 目前的
大饼干（`BigCookie.razor`，纯 emoji 🍪）缺少这一层。本设计补上它。

目标：买了 Cursor 后，饼干背后浮现一圈手指，绕饼干缓慢旋转、此起彼伏地戳击
饼干；数量随 Cursor 增长；数量极大时用颜色分档表达，而不是无限堆 DOM。

## 关键技术约束（为什么这样设计）

- **原版用 `<canvas>` 一次性绘制所有光标**，几百个只是几百次 draw call，GPU
  无压力，因此可以真·不封顶（实测 10+ 环）。
- 本 remake 的饼干是 **DOM + CSS** 方案：每根手指是独立 DOM 元素，各自跑 CSS
  动画。DOM 元素越多，浏览器 layout/paint/composite 成本线性上涨，几百个持续
  动画的元素在低端机会掉帧。**这与 WASM 快慢无关**——WASM 只管 C# 算 CPS，
  渲染手指是浏览器渲染层的事。
- 因此选择：**保持 DOM 方案，用"颜色分档合并"把屏上元素数锁死在有限范围**，
  用颜色档位承载"无限增长"的表达。这既解决 DOM 爆炸，又符合玩家"越买越壮观"
  的期待。

## 组件结构与数据来源

- 新增独立组件 **`CursorRing.razor`** + **`CursorRing.razor.css`**，放在
  `Game.Web/Components/`，与 `MilkLayer` 平级。
- `Home.razor` 在 `cookie-area` 内、`MilkLayer` 与 `BigCookie` 之间插入
  `<CursorRing State="Save.State" />`。手指叠在饼干**背后一圈**（`z-index:1`），
  露出来但不挡点击。
- 遵守项目铁律 **UI → domain 单向**：组件只接收 `[Parameter] GameState State`，
  从 `State.BuildingCounts.GetValueOrDefault(BuildingId.Cursor)` 读 Cursor 数量。
- **不在 UI 写任何游戏规则**：手指数量、颜色档位都是"根据 Cursor 数量算出的
  纯展示派生值"，属于视图逻辑，放在组件 `@code`（不污染 `GameState`）。
- `Home.razor` 每 tick 重渲染（现有机制），手指数量随购买**实时更新**，无需
  额外订阅。
- 组件**无状态、无计时器**：旋转与戳击都是纯 CSS 动画；C# 只负责算"这一帧该画
  哪些手指、什么颜色、在什么角度"。好处：不增加 `GameLoop` 负担、无需 JS
  interop、可被 Blazor 快照测试。

## 数量 → 手指/颜色的派生算法

核心是一个**静态纯函数**：输入 `cursorCount`，输出要画的手指列表（每根带
颜色档位 + 所在环 + 环内序号）。

**第一步 · 铺满 5 环（白手指）**
- 每环容量随半径递增：第 1 环 16，之后每环 +8 → **16 / 24 / 32 / 40 / 48**，
  5 环共 **160 个白手指位**。
- `cursorCount ≤ 160` 时：直接画 `cursorCount` 根白手指，从内环往外依次填充。
  覆盖绝大多数早中期游玩。

**第二步 · 满 160 后 2:1 进位合并升色**
- 超出部分按 **2:1 进位**升级颜色，档位序列
  `白(0)→绿(1)→蓝(2)→紫(3)→橙(4)→红(5)→金(6)`。
- 本质是把 `cursorCount` 写成"以 160 为基、每档 ×2"的分档计数：贪心从最高档
  往低档拆，保证任意时刻屏上手指总数 ≤ 160，颜色分布反映数量级。
- 实现时用一个带 `<summary>` 注释的小纯方法写清推导（符合项目"派生数学带
  why 注释"约定）。
- 触发合并是**极低频里程碑**：首次要 160 个 Cursor，之后每次进位所需增量翻倍
  （160 → +160 → +320 → +640 …）。因 Cursor 单价指数增长，整局顶多触发个位数
  次。

**封顶保护 —— 渲染层真正的上限**
- 若颜色升到**金色仍超容量**（天文数字），**不再合并也不再增加**：画满 160 根
  金手指封顶，屏上 DOM 恒定 160 个元素。这是渲染层的真正上限。

> 关键取舍：**屏上 DOM 元素数恒定 ≤ 160**，无论 Cursor 是 10 还是 10³⁰，性能
> 上限锁死；颜色档位承载增长表达。

## 布局与定位（环绕几何）

- 根节点：绝对定位、居中于饼干的正方形容器 `cursor-ring`
  （`position:absolute; inset:0; z-index:1; pointer-events:none`）——手指纯装饰，
  绝不拦截饼干点击。
- 每根手指是容器内绝对定位元素，用 CSS 变量驱动，C# 只吐数据：
  - `--angle`（环内角度）、`--radius`（所在环半径）、`--phase`（戳击相位延迟）、
    `--color`（7 档颜色值）。
- **定位**：`transform: rotate(var(--angle)) translateY(var(--radius))`——先绕
  中心转到角度，再沿半径推出去，天然均匀环绕；手指自身再叠一层 `rotate` 让
  指尖朝向圆心。
- **角度分配**：每环 `n` 根，第 `i` 根角度 = `360°/n × i`；相邻环角度错开半格，
  避免径向对齐呆板。
- **半径**：饼干半径约 7.5rem（emoji 15rem），第 1 环半径约 8rem（饼干边缘外
  一点），每环 +1.6rem 向外扩；移动端（emoji 缩到 10rem）用媒体查询等比缩小
  这套变量。
- **颜色**：7 档用一组 CSS 变量集中定义（`--cursor-white/green/blue/purple/
  orange/red/gold`）。

**手指外观**：内联 **SVG** 手指路径，`fill` 引用 `var(--color)`，描边用同色系
深色。选 SVG 而非纯 CSS 形状，因为：形状更像"手"、7 档上色精准且可做描边/高光、
矢量缩放不糊；运行时开销与 CSS 方案相同。**自己画通用手指路径**，不复制原版
素材（符合 NOTICE.md）。

> 关键点：几何全靠 CSS transform + 变量，C# 传纯数字；旋转动画只需给容器加
> `@keyframes spin`，整圈手指跟着转，无需逐帧计算坐标。

## 动画（旋转 + 戳击）

两层独立纯 CSS 动画叠加：

**第一层 · 整圈旋转**
- `cursor-ring` 容器：`@keyframes spin { to { transform: rotate(360deg) } }`，
  `animation: spin 60s linear infinite`（约 60 秒一圈，慢而不晕）。方向、速度用
  CSS 变量留出以便微调。

**第二层 · 每根戳击**
- 手指内层元素做"伸出→缩回"：`@keyframes poke`，用 `translateY` 让指尖朝圆心
  短促推进再弹回（约 1.2s 周期）。
- **相位错开**：用 `--phase`（按手指全局序号取模映射到 0~1.2s 的
  `animation-delay`）让手指此起彼伏地戳，而非整齐划一——这是原版"一群自动
  点击器"观感的灵魂。

**与真实点击解耦**
- 饼干现有 `bounce`（点击缩放 80ms）不变。手指戳击是**独立自转的装饰，不跟
  真实点击同步**——它代表被动 CPS，不是玩家点击，语义上不应联动。原版亦如此。

**性能与无障碍**
- 尊重 `prefers-reduced-motion: reduce`：该模式下**关闭旋转与戳击**（手指静止
  排布），避免前庭不适，符合项目无障碍取向。
- 动画 GPU 友好（只动 `transform`），160 元素恒定，低端机可接受。

## Hover Tooltip（自我解释）

手指环不是哑装饰，像 `MilkLayer` 一样可查询、能自我解释。

- 复用现有 Tooltip 设施：`@onmouseover` → `TooltipService.Show(BuildTooltip,
  elementRef)`，`@onmouseout` → `Tooltip.Hide`。定位由
  `cookieClicker.positionTooltip` JS 助手贴着 anchor 浮出（`position:fixed`，
  不被裁剪）——与 milk tooltip 同一套，位置贴在触发元素旁。
- 遵守项目 **Tooltip 实时刷新契约**：内容用 builder **每次现算**、不缓存
  fragment，`Game.Web.Tests` 守护。合并档位本就每帧从 `cursorCount` 现算，
  天然满足；hover 时永远显示当前真实状态。
- 内容（沿用 `TooltipCard`，与 milk tooltip 视觉一致）：
  - 标题 + 图标：🖐️ + "光标环"
  - 说明：一句话讲这是什么——"每个光标每秒替你点击饼干；它们绕着饼干自动
    工作"
  - 数据行：当前光标总数；当前最高颜色档位及含义（如"金色 = 每个代表 64 个
    光标"）；距下一次合并还差多少个光标（给玩家目标感）
  - 风味文案：一句原创吐槽

> 用 hover tooltip 替代了曾考虑的"合并里程碑模态弹窗"方案——弹窗需持久化"已弹
> 档位"计数器、bump 存档版本，被判定为"脏"。tooltip 方案保持**零 domain 改动、
> 零存档影响**。

## 边界与可见性

- `cursorCount == 0`：不渲染任何手指（组件输出空），饼干周围干净——买第一个
  Cursor 才出现第一根手指，强化"购买有视觉反馈"。
- 手指列表用稳定 `@key`（全局序号），Blazor diff 增量增删，不整体重建，购买时
  新手指平滑加入。
- 层叠：手指 `z-index:1`、饼干 `z-index:2`、milk 在饼干区底部——实现时核对
  milk 的具体 z-index 微调，确保三者不打架。

## 测试

- 纯派生算法（`cursorCount → 手指档位分布`）抽成**静态纯函数**放可测位置，用
  **`Game.Web.Tests`** 覆盖关键用例：0 根、边界（160、161）、进位点（满一档
  触发升色）、天文数字封顶到 160 金手指。与现有 `Game.Web.Tests`（tooltip
  契约那套）一致。
- 组件快照测试（可选）：验证"0 Cursor 不渲染手指""N Cursor 渲染正确数量的
  手指元素"。

## 改动清单

- 新增 `Game.Web/Components/CursorRing.razor` + `CursorRing.razor.css`
- `Home.razor`：`cookie-area` 内、`MilkLayer` 与 `BigCookie` 之间插入
  `<CursorRing State="Save.State" />`
- `Game.Web.Tests`：加派生算法测试
- **无 `Game.Core` 改动、无存档迁移**（纯展示，不持久化）
- 若涉及新增 i18n 文案（tooltip 标题/说明/风味），按现有本地化流程补齐
  中英文条目
