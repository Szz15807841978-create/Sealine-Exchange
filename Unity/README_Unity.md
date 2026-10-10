# 当前 Main：用户布局 + 木筏实体观测（2026-10-06）

## 新增 UI 的纸墨皮肤

新增玩家 UI 和木筏观测对话框使用实心纸底、墨色文字、深青盈利/曲线、朱红风险。范围包含电台展开框、帮助/账本、结算、调试及其下拉列表、返回/重置确认、停针/手速挑战。使用 `Assets/Art/UI/Skin/` 中的 `panel_paper`、`panel_inset`、`button_paper`、`button_amber`；**不使用**中心透明的 `panel_paper_flat`、`panel_paper_detail` 等边框素材。全屏遮罩和透明点击区域保留原透明语义，实际面板均为实心。

这次换肤不创建备份、不调用旧场景生成器、不改物件位置/缩放/层级/显隐、文字内容、3D 装置或玩法参数。`PaperInkUiScope` 仅用于阻止旧皮肤脚本在启动时覆盖这些控件；实际 Sprite、颜色仍保存在每个可编辑 UI 对象上。Inspector 中 `PlayerMarketView` 的 Positive/Negative/Gauge 色和 `PlayerMarketChart` 的曲线/网格/事件色控制动态显示，不改变玩法。

换肤工具：`Assets/Editor/PlayerPaperSkinSetup.cs`，菜单 `Fishing / 玩家版 / 仅替换新增 UI 为实心纸墨皮肤（不备份、不改布局）`。仅在显式请求时运行；未保存或 Play 状态不会覆盖场景。应用前后逐组件比对，除了列出的外观字段和皮肤保护标记，其余序列化数据必须完全一致才保存。报告：`tools/PlayerPaperSkin-report.txt`。手调后无需再次运行换肤菜单。

### 天空调试 UI 与警告修复

`SkyDebugPanel` 的 Awake/OnEnable/OnValidate 只标记待绑定，由 Start/Update 复用现有控件；编辑器刷新不会删除重建子物件、改动布局、重置显隐或自动刷新选中颜色。只有运行时或手动展开一个尚不存在的面板才创建默认控件。已有按钮只重新绑定该脚本自己的回调，不清空 Inspector 和其他脚本注册的事件；运行时仍支持 F1 和八种天空切换，编辑器通过 Inspector 切换天空。

`PlayerUiRepairSetup` 仅清理两处已停用“木筏水线”的 Missing Script 引用，不删除物件、Renderer 或其他有效组件，不生成备份；使用路径、文件 ID 和停用状态核对目标。清理前后逐组件核验，并重复调用天空面板验证/绑定，要求场景布局、天空设置、显隐均不变且不会因此变脏。报告位于 `tools/PlayerUiRepair-report.txt`。

Play 验证检查纸墨二级面板、天空按钮复用和木筏往返，结束后恢复编辑器场景及测试中临时更改的共享天空/海面材质，真实存档不动。第三方素材中的未使用字段或过时 API 编译提示不通过删除资源或屏蔽警告来掩盖。

Main 的备用 `EventSystem` 组件保存为禁用，由 `PlayerMarketController` 在独立启动时启用；叠加到木筏时沿用木筏的输入系统，避免加载瞬间重复注册。物件和位置未变，不要手动把 Main 的该组件改为默认启用。

2026-10-06 最终验证通过 80 项运行断言，另有 5 项材质恢复/文件不变/相关警告为零的检查。天空面板编辑模式重复验证 20 次不重建、不弄脏场景；木筏 Missing Script 数量为 0。纸墨换肤覆盖 87 个 Image、148 个 Text，包含隐藏二级面板。最新完整记录为 `tools/PlayerRaftPlayCheck.txt`，界面预览为 `tools/PlayerRaftPlay-main.png`、`tools/PlayerPaper-*.png`。Console 中修复前的旧警告需手动 Clear 才会从列表移除，不能据历史累计数判断修复后的新增警告。

## 上一轮布局迁移记录

`Assets/Scenes/Main.unity` 使用原生 Unity 玩家规则，不是 WebView。本轮以用户保存后的布局为准局部修改，没有运行 Main 全量生成器。用户修改前快照与木筏场景备份位于 `tools/backups/before-raft-observations-20261006-160753/`；更早的旧 Main 在 `tools/backups/before-player-main-20261006/`。Main 原 `.meta` / GUID 保留。

## 调整 UI 和布局

打开 Main，在 `Player Main · 玩家捕捞 / Player Canvas · 可编辑界面` 下修改：

- `01_Header`：标题、资源卡、说明及返回按钮。
- `03_Market`：曲线、独立坐标文字和 `Challenge` 小游戏。保持用户右侧上方位置 `(962,-184)`，实际宽度 `926.4`；将横向 0.8 缩放烘焙为控件宽度/间距，字体不再横向压缩。
- `04_Trading`：本金、诱饵、收网比例、追加/收网按钮和冷却条。保持右侧下方位置 `(962,-612)` 与同样宽度，不改用户文案。控件实体仍可逐个调整。
- `07_Radio`：左下角折叠栏，默认收起；点击展开/收起，记录在其 `Expanded` 子对象内。位置、展开窗口尺寸、标题按钮全部可编辑。
- `02_Exploration`、`05_Island`、`06_Rumor`、`11_Survey` 已从 Main 移除，对应调查/区域/生态/信息变量没有删除。
- `09_Result`、`10_Help`、`12_Debug`、`13_Return`、`14_Reset`：原有结算及弹窗。账本位于 `10_Help` 内，包含 30 个预建记录行；电台有 6 个预建行，均可滚动。其他说明与用户的布局改动保留。

这些对象在编辑模式下就存在；可调整 RectTransform、字号、颜色和文案。运行时只更新状态、数字、显隐和动态填充，不运行布局重建、不覆盖面板的位置尺寸。需要预览弹窗时可在编辑器临时激活，完成后关回去。不要为了调整布局重新运行生成菜单：该菜单会重建 Main（并先备份），覆盖此后手调的 UI。

CanvasScaler 参考分辨率为 1920×1080。图表的线宽、颜色在 `PlayerMarketChart` Inspector 内，坐标文字独立可调；曲线采样和指针/区间位置属于动态数据。`PlayerMarketView` 保存全部对象引用，重命名或移动现有对象不会断开引用；删除/替换对象后需更新引用。内置中文字体按本机字体加载；给 Text 指定自己的字体资源会保留，不会被替换。

## 玩法与调试

原生规则包含：整数 kg、唯一永久网、天气/温风/生态/培育/独立诱饵、五类形态、事件、T+0 追加和重新下网、1–100% 分批收网、双边 0.5% 收市统一缴费、90 秒和每日两场。调查归入未来搜打撤，Main 不再以“点击调查”为开市门槛，不会伪造调查结果。用户已确认 Demo 暂时默认有一张网；正式接入搜打撤后，关闭 Main 和木筏组件的 `DemoNetAvailable`，由真实搜打撤结果提供有网状态。玩家不会看到最适值、增益百分比或模型公式。

小岛面板移除后，局间使用清洁度 80、培育 0 的默认值；不会重置正在运行中的行情。两项变量及计算公式保留。正式接入小岛后关闭 `UseIslandDefaults`，通过 `ReceiveIslandState` 同步。

第一次挑战按 Q 停针，第二次在 6 秒内交替 A/D；24 次零增益、60 次满增益，处罚/奖励范围不变。Space 暂停，A 收网。挑战中 A/D 不会触发收网或调试。

隐藏调试入口：`Ctrl+Shift+D`。保留用户对左上标题的修改，不恢复旧“潮”图标。包含活鱼/诱饵补给、环境输入、形态选择、挑战/事件、后台参数和带确认的重置。配置资源为 `Assets/PlayerFishing/PlayerMarketSettings.asset`，可在 Inspector 调整数值。请以新开局验证参数变更。

## 木筏与 Main 的连接

木筏场景仍是 `Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity`。本轮只新增观测装置及对话框，原有物件 Transform、浮力、角色和天空配置保持。原有“雨天早上 → 木筏右侧 → 确认 → 镜头平移 → Additive 加载 Main”保留。Main 的相机/EventSystem 由原桥接关闭，叠层继续使用木筏背景。

新增对象位于 `3D木筏与装置_全部可编辑 / RaftRoot / 捕捞观测装置 · 可编辑 3D 与会话`：风向标、挂在筏下的温度计、海况观测牌、渔讯通讯器。每个模型由已保存的子物件组成，Collider 负责点击；可拖动/替换模型。使用现有木筏渲染层，不修改海洋相机的 culling mask。数字/提示面向相机，并补偿现有 RenderTexture 的水平镜像。

点击装置，或靠近后按 F，打开已有 UI 对话框；Esc / 关闭退出。风向标显示风速；温度计显示水温；观测牌显示目标水域天气、温风并在开市前选择地点；通讯器提供原 NPC 信息。都读取与 Main 同一份会话，不在读取时随机重抽。Main 加载后禁止切换地点；未结算持仓返回木筏也不能换地点。

`捕捞观测对话框 · 可编辑 UI` 在原木筏 Canvas 下。打开时暂停行情并临时锁定角色输入，关闭后恢复之前的输入状态；不会意外解开捕捞中的角色锁定。数据是目标水域的海况，原木筏本地雨天/早晨触发门槛不变。观测不会改写原天空调试器。

`RaftMarketStation` 的接口：`SelectDestination(index)` 在木筏开市前选地点；`ReceiveUnderwaterFindings(index, acquiredNet, estimate, quality, samples)` 接收真实调查和网；`ReceiveIslandState(cleanliness, cultivation)` 接收小岛生态。新档用 `SelectedWater` 作为初始地点，存档及进行中的场次保留已选地点。

Main 右上角“返回木筏”会先暂停、保存，再只卸载 Main；桥接将镜头移回进入前位置，恢复角色、天气与操作。从独立 Main 启动也可进入木筏。木筏已加入 Build Settings，其他场景条目保留。返回不提前结算、不重抽行情，进行中的挑战也保留；再次进入 Main 后手动继续。

新存档键为 `Fishing.PlayerMarket.Unity.v1`，独立于 HTML、旧版捕捞和木筏存档。Unity JSON 使用显式存在标记保存空持仓/空调查/空挑战，防止反序列化生成虚假对象。测试使用独立临时存档，不更改真实玩家存档。

## 代码与校验入口

- `Assets/Scripts/PlayerFishing/PlayerMarketModel.cs`：不依赖 Unity 的规则层。
- `PlayerMarketController.cs`：绑定场景引用、输入、刷新与存档。
- `PlayerMarketView.cs` / `PlayerMarketChart.cs`：可编辑视图引用与曲线。
- `PlayerMarketSave.cs` / `PlayerMarketSettings.cs`：存档和可调配置。
- `Assets/Editor/PlayerMarketSceneBuilder.cs`：仅编辑器使用的生成器。
- `RaftMarketStation.cs` / `RaftObservationTarget.cs`：共享会话、实体交互、目的地和未来系统数据交接。
- `Assets/Editor/PlayerRaftIntegrationSetup.cs`：局部迁移工具，不重建 Main；只在显式请求时执行一次。
- `Assets/Editor/PlayerRaftIntegrationPlayCheck.cs`：在保存后的现有场景上验证，使用临时存档，结束后恢复编辑器场景。菜单 `Fishing / 玩家版 / 验证木筏观测与现有布局（不重建场景）`。
- `Assets/Editor/PlayerMarketPlayCheck.cs` 是旧版全量生成测试，当前布局下已禁止执行，避免覆盖手工调整。
- `tools/run-playercheck.ps1`：真实 Unity 程序集编译、1,510 项纯规则断言、455 个 HTML/C# 曲线样本及 5 份结算对照。

最新场景报告为 `tools/PlayerRaftIntegration-report.txt`，往返验证为 `tools/PlayerRaftPlayCheck.txt`；预览为 `tools/PlayerRaftIntegration-preview.png` 和 `tools/PlayerRaftPlay-*.png`。以最新报告结尾判断检查是否通过。`PlayerMain*` 报告是先前版本留档。

上一轮布局迁移通过 51 项实际 Play 模式检查，包括镜像画面的装置点击映射、读数不重抽、目的地锁定、同一会话共享、折叠电台、T+0、往返与结算；两个场景在测试中没有重新保存，真实玩家存档未动。最新纸墨 UI / 警告修复验证结果见本文开头。

批处理测试的截图使用离屏 UI 合成（木筏背景使用其现有海洋 RenderTexture），不是 Unity Game 窗口截屏；截取后恢复所有临时渲染设置，不保存木筏场景。

---

# 以下为旧捕捞版本的历史文档

下文仅为留档，涉及网损耗、线虫、旧 Runtime/Sim、旧 UI 和旧验证状态的说明不适用于当前 Main；旧脚本保留以兼容其他场景的引用，不会在玩家版 Main 上启动。

# 捕捞 · 雨天市场博弈 —— Unity 复刻版

把同目录 `../index.html`（单文件网页 Demo，1940 行）1:1 复刻成 Unity 工程。
**规则、数值、曲线形态、仪表盘、结算口径全部对齐网页版**，差异只在表现层（网页用 Canvas 2D + DOM，Unity 用 uGUI + 程序化网格）。

---

## 一、怎么打开

1. 把这个 `Unity` 文件夹当作工程根目录（里面已经有 `Assets/`、`Packages/`、`ProjectSettings/`）。
2. **不要**用 Hub 的「New project」重建 —— 直接 **Add project from disk** 指向这个 `Unity` 文件夹，
   用 **Unity 2022.3.62f3c1**（和 `ProjectSettings/ProjectVersion.txt` 一致）打开。
   Unity 会自己补齐其余 `ProjectSettings/*.asset`，不需要手工配置。
3. 打开 `Assets/Scenes/Main.unity`。默认保持可直接 Play 的运行时版本。
4. 直接按 **Play**。

> **当前场景化进度**
> 执行 Unity 菜单 `Fishing → 场景化 → 生成第一阶段场景对象` 后，工具会把基础设施与
> “上绿下红”的曲线预览转换为真实场景对象；不用 Play 就能在 Hierarchy 和 Game/Scene
> 视图中看到。其余完整玩法界面暂时仍在运行时由 `SceneGameHost`
> 调用 `GameController` 生成，再由 `MainLayout` 把
> 状态栏 / 曲线 / 数据条 / 持仓条 / 操作区 / 仪表盘 / 遮罩 / 二级窗口 / 飘字 全部搭出来。
> 后续可以按面板逐块替换为可拖拽、可序列化的场景对象。

如需手动重新生成第一阶段对象，使用 Unity 菜单：
`Fishing → 场景化 → 生成第一阶段场景对象`。

不需要任何额外资源包：字体用系统里的 **Microsoft YaHei**（没有就依次回退），图标用的是文字符号。

---

## 二、工程结构

```
Unity/
├── Assets/
│   ├── Scenes/Main.unity            ← 空场景（只负责被打开）
│   └── Scripts/
│       ├── Runtime/                 ← 规则层：不依赖任何 UI
│       │   ├── Bootstrap.cs         ← 运行时自举（相机 / Canvas / EventSystem / GameController）
│       │   ├── Cfg.cs               ← 对应 index.html 顶部的 CFG
│       │   ├── Sim.cs               ← ★ 全部捕捞规则（结算/丢网/修补/线虫/场次流转/仪表盘推进）
│       │   ├── FishDefs.cs          ← 鱼类表 + 网的颜色轮换
│       │   ├── Patterns.cs          ← 13 种股市形态关键帧
│       │   ├── Rng.cs               ← Math.random / randInt / rand / randn（Box-Muller）
│       │   ├── Num.cs               ← clamp / smoothstep / lerp / f1 / f2 / signed
│       │   ├── PathMaker.cs         ← buildPath / sampleKeys / makeNoise + 冲击叠加层
│       │   ├── GaugeKit.cs          ← 仪表盘布局 / 指针 / 判定 / 冲击
│       │   ├── Loot.cs              ← splitValue / decompose / grantLoot / awardBag
│       │   ├── GameState.cs         ← 对应 index.html 的全局状态 S
│       │   ├── Palette.cs           ← :root 那批 CSS 变量的 Unity 版
│       │   ├── FontUtil.cs          ← 中文字体选择与兜底
│       │   └── UiKit.cs             ← 建 RectTransform / Image / 渐变 / 布局组的小工具
│       └── UI/                      ← 视图层（只读状态，不含规则）
│           ├── MainLayout.cs        ← #app 网格 + 行高布局
│           ├── ChartGraphic.cs      ← 自绘网格用的 Graphic（网格/阈值线/面积/折线）
│           ├── ChartView.cs         ← drawChart() 的完整移植
│           ├── HudView.cs           ← 顶部状态栏
│           ├── StripView.cs         ← 持仓条 + 操作区（含收网 CD 条）
│           ├── GaugeView.cs         ← 仪表盘小玩法
│           ├── Widgets.cs           ← 按钮 / 数值框 / 鱼篓胶囊
│           ├── WindowFrame.cs       ← 卡片式窗口骨架
│           ├── TableBlock.cs        ← 表格 + 破损网状态区 + 鱼篓区
│           ├── OverlayViews.cs      ← 开盘前 / 收市结算 / 傍晚 / 渔获窗口
│           ├── PosWindowView.cs     ← 持仓窗口（多选收网 / 中途加网）
│           ├── ToastView.cs         ← 底部飘字
│           └── GameController.cs    ← 主循环 + 输入 + 视图刷新（规则全部委托给 Sim）
├── tools/                           ← 校验工具（不在 Assets 下，Unity 不会导入）
│   ├── run-rulecheck.ps1            ← 一条命令跑完规则校验
│   └── RuleCheck/
│       ├── UnityShim.cs             ← 无头测试用的 UnityEngine 最小替身
│       └── RuleCheck.cs             ← 414 项规则断言
├── Packages/manifest.json
├── ProjectSettings/ProjectVersion.txt
└── README_Unity.md                  ← 本文件
```

> **规则为什么单独抽一层 `Sim.cs`？**
> 一是 `GameController` 只管"驱动 + 显示"，规则集中在一处；
> 二是 `Sim` 只依赖 `GameState / Cfg / Rng / Loot / PathMaker`，
> 于是可以在**没有 Unity 的情况下**直接跑断言（见第七节）。


---

## 三、操作

| 操作 | 说明 |
| --- | --- |
| `空格` | 暂停 / 继续（**只有暂停时才能收网和下网**）；开盘前按 = 直接开始 |
| `Q` | 停下仪表盘指针 |
| `R` | 打开持仓窗口 |
| `A` | 全部收网（暂停时） |
| `E` | 结束今天的捕捞 |
| `F` | 打开渔获窗口 |
| `Enter` | 开盘前直接开始 |
| `Esc` | 关窗口 / 取消暂停 |
| 右下角 🔧 | 调试面板：加网 / 加线虫 / 加渔获 / 弄破网 / 弹仪表盘 / 立即收市 / 重开 |

一切和网页版一致，包括「暂停不盖遮罩」（曲线、持仓条、实时数据全程可见）。

---

## 四、规则实现对照

网页版的规则逻辑在 `index.html` 的 `<script>` 里，函数名与 Unity 里的对应关系：

| index.html | Unity | 说明 |
| --- | --- | --- |
| `CFG` | `Cfg` | 参数一一对应，含 `GAUGE_*` / `SHOCK` |
| `S`（全局状态） | `GameState` | 字段名基本同名 |
| `randn` | `Rng.Randn` | Box-Muller + spare，逐位等价 |
| `sampleKeys` | `PathMaker.SampleKeys` | smoothstep 插值 |
| `makeNoise` | `PathMaker.MakeNoise` | 均值回归随机游走，±30% 夹取 |
| `buildPath` | `PathMaker.Build` | 形态幅度 `100+(v−100)×1.3`，开盘价固定 100 |
| `shockFactor` / `addShock` | `MarketPath.ShockFactor` / `GaugeKit.AddShock` | **基准路径 `Arr` 永不修改**，冲击只乘在生效价上 |
| `makeGaugeLayout` / `gaugeKindAt` / `gaugeNeedle` / `planGauges` | `GaugeKit.MakeLayout` / `KindAt` / `Needle` / `Plan` | 宽度固定、位置每次洗牌 |
| `judgeGauge` / `gaugeTick` / `pressGauge` | `Sim.JudgeGauge` / `GaugeTick` / `PressGauge` | 不按 = 判为差；暂停时指针冻结 |
| `splitValue` / `decompose` / `fishBagValue` / `addBag` | `Loot.SplitValue` / `Decompose` / `BagValue` / `Merge` | 价值守恒，随机把大鱼拆成小鱼 |
| `grantLoot` / `awardBag` | `Loot.GrantLoot` / `AwardBag` | 线虫单独进材料池 |
| `settleNet` | `Sim.SettleNet` | 丢失 / 破损 / 持平 / 盈利 + 回收网数 + 网费 + 副产物线虫 |
| `autoRepair` | `Sim.AutoRepair` | 优先补最接近修满的，1 条线虫 = 5% |
| `addNet` / `startCD` / `startSession` | `Sim.AddNet` / `StartCD` / `StartSession` | 下网只花 1 张；CD 3~5 秒随机 |
| `tryCollect` / `collectAll` / `collectSelected` / `castNets` | `Sim.*`（同名） | 都算一次 CD |
| `closeMarket` / `endDay` / `skipSession` / `nextSession` / `toEvening` / `nextDay` | `Sim.*`（同名） | 收市即全部结算，不跨场 |
| 主循环 `loop` | `Sim.TickSession` + `Sim.GaugeTick` + `GameController.Update` | 冻结判定在 `TickSession` 里 |
| `drawChart` | `ChartView.Draw` + `ChartView.BuildMesh` | 网格 / 阈值线 / 买入线 / 丢失线 / 面积 / 涨绿跌红折线 / 入场标记 / 脉冲点 / 价格标签 |
| `renderHUD` / `renderNets` / `renderControls` / `renderOverlay` / `renderPosWindow` | `HudView.Refresh` / `StripView.Refresh` / `ControlsView.Refresh` / `RefreshOverlay` / `PosWindowView.RefreshRowStates` | 每帧刷新 |
| `statusOf` / `netResultText` | `GameController.StatusOf` / `NetResultText` | 文字与颜色逐条对齐 |
| `devAction` / `resetGame` | `GameController.DevAction` / `ResetGame` | 调试面板 |

> `GameController` 只做三件事：读输入、调 `Sim`、刷新视图。规则一行都不在里面。


### 关键口径（都照搬，没有改）

- 1 张网 = 1 手 = 100 价值单位；下网永远只花 1 张网，买入价 = 当时市价 ×(1+FEE)。
- `回收网数 = ⌊卖出价 ÷ 买入价⌋`，至少 1 张；多回收的按 100/张 从收益里扣。
- **丢失线是买入价的 70%**（收益率 ≤ −30%），不是固定的 50；跌破只算"风险"，收网/收市那一刻才永久丢失。
- −30% < 收益率 < 0 → 按亏损比例破损（完整度 = 卖出价÷买入价），结算后立刻自动扣线虫补满。
- 每张成功结算的网产 3~6 条副产物线虫；卖出价 ≥ 200 再 +5 条。
- 收网 CD 3~5 秒，暂停期间不流逝。
- 一个雨天两场，每场 90 秒，第二场重新随机且开盘价仍 100。
- 每场 2 次仪表盘：最佳 8% / 次佳 12%×2 / 普通 26%×2 / 差 8%×2，位置洗牌；
  最佳 +12%（4 秒爬升）、次佳 +5%、普通不变、差 −15%（0.9 秒砸下）；5 秒不按自动判差。

---

## 五、和网页版的已知差异（只在表现层）

| 项 | 网页版 | Unity 版 |
| --- | --- | --- |
| 曲线渲染 | Canvas 2D，面积用垂直渐变 | 自绘 `Graphic` 网格；面积用**顶点色在"曲线点→基线"之间插值**近似渐变 |
| 背景 | `radial-gradient` 光晕 | 纯色 + 面板纵向渐变 |
| 圆角 / 阴影 | CSS `border-radius` / `box-shadow` | uGUI 直角矩形 + `Outline` 描边（无圆角、无阴影） |
| 鱼 / 线虫图标 | emoji | 同样是 emoji（鱼篓胶囊里）。系统字体缺 emoji 字形时那一个字符会显示成方块，功能不受影响；右下角调试按钮已改成中文「调试」以免整颗按钮看不见 |
| 破折号 | `—` / `−` | 同 |
| 字体 | 系统 UI 字体栈 | `Microsoft YaHei` → 黑体 → Noto → Segoe UI → Segoe UI Emoji 逐级回退 |

数值与规则**没有任何差异**。

---

## 六、想调参数改哪里

全部集中在 `Assets/Scripts/Runtime/Cfg.cs`，和网页版 `CFG` 一一对应：

```csharp
Duration = 90f           // 单场时长
SessionsPerDay = 2       // 一个雨天几场
Fee = 0f                 // 买入手续费：买入价 = 市价 × (1+FEE)
StartNets = 1            // 初始完整网
CdMin / CdMax = 3f / 5f  // 收网 CD 区间
MidLine / BonusLine = 100f / 200f
BonusWorms = 5
NetValue = 100f          // 1 张网的价值
WipeRatio = 0.7f         // 跌到买入价的 70% 收网 → 永久丢失
RepairPerWorm = 5f       // 1 条线虫恢复 5% 完整度
WormByproductMin/Max = 3 / 6
LootScale = 1f

VolSigma = 0.05f         // 噪声强度
VolClamp = 0.30f         // 噪声上下限 ±30%
PatternScale = 1.3f      // 形态幅度拉伸

GaugePerSession = 2
GaugeWindow = 5f         // 到点没按 = 判为差
GaugeSweep = 1.4f        // 指针单程扫过时间
GaugeFreeze = true       // 未判定时冻结市场
GaugeBest = 8f
GaugeShuffle = true
GaugeHalf                // 各段宽度
GaugePlan                // 两次触发的随机时间窗
ShockOf(kind)            // 各区间冲击幅度与爬升时间
```

界面配色在 `Assets/Scripts/Runtime/Palette.cs`（对应网页版的 `:root`）。

---

## 七、验证状态

### 已完成的验证

**① 整个工程编译通过（用 Unity 自带编译器实测）**

本机装了 Unity 2022.3.62f3c1 但当时没有可用的终端，所以在拿到命令执行能力后，
用 Unity 自带的 Roslyn + Unity 的引用程序集做了真实编译：

```
26 个源文件 → 0 error / 0 warning，产出 Fishing.dll（98 KB）
```

**② 规则层 416 项断言全部通过（无头运行，不需要 Unity）**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-rulecheck.ps1
```

它把 `Runtime/` 里的纯逻辑文件 + 一个 `UnityEngine` 最小替身编成一个普通 .NET 程序，
直接跑断言。当前结果：**416 / 416 通过**，覆盖：

| # | 覆盖内容 |
| --- | --- |
| 1 | 基本单位与初始状态（1 张网 = 100、初始 1 张、90 秒、一个雨天两场） |
| 2 | 结算表（README 的 5 个丰收例子：150/240/200/50→150/210 的回收张数与资源） |
| 3 | 丢失线是**买入价 ×0.7**（100→70 丢、150→105 丢、150→106 只破损） |
| 4 | 破损与自动修补（−25% → 扣 5 条线虫补满；线虫不够只补多少算多少；优先补最接近修满的） |
| 5 | 渔获分解与价值守恒（`decompose` 对 0~400 全部守恒；线虫进材料池不进鱼篓；零头结转） |
| 6 | 曲线生成（13 种形态、开盘价恒为 100、冲击叠加层不动基准路径、爬升插值） |
| 7 | 仪表盘（区段宽度固定且位置洗牌、指针三角波、不按=判差、暂停时推进函数也完全冻结、继续后恢复、判定后解冻） |
| 8 | 场次流转（开盘扣网、中途加网按当时价、CD 3~5 秒且暂停不流逝、收市自动结算、跳过一场不跨天、结束今天用当时价） |
| 9 | 连跑 6 天 × 2 场：逐场渔获账目守恒 + 实体网去向唯一 + id 单调不复用 |
| 10 | README 点名的全部具体例子逐条复算 |

**③ 编译与测试抓出的真实 bug（已修）**

| 问题 | 影响 |
| --- | --- |
| `Loot.SplitValue` 字段名与方法名撞车 | 编译不过 |
| `Loot.Add()` 里 `if (n <= 0) return;` 把"减掉一条大鱼"的负数静默丢弃 | **拆分大鱼时渔获价值凭空翻倍** |
| `ChartGraphic._v` 是 `readonly` 却改其成员、`Gradient2` 用了不存在的 `rectTransform` 属性等 11 处 | 编译不过 |
| `Toggle.onValueChanged` 回调签名不匹配、闭包引用未声明的局部变量 | 编译不过 |
| `WormsUsed` 一开始写成了"线虫差值 ÷ 5" | 口径错误；正确口径是 `ceil(破损% ÷ 5%)` |
| 自动仪表盘只有状态、没有同步显示，且指针没有逐帧刷新 | 自动弹出、指针、超时判定和关闭现在全部由 `GameState` 统一驱动 UI |
| `GaugeTick` 在暂停状态仍会推进 | 暂停期间指针、超时窗口和判定停留时间全部冻结 |
| `MainLayout.FindCanvas()` 静态访问实例字段 | Unity 运行时程序集编译失败；已改为实例方法并通过完整脚本编译 |
| 仪表盘和持仓条在窄窗口或多持仓时溢出 | 仪表盘按父容器 86% 缩放；持仓胶囊改为带裁切的横向滚动区 |

### 还没做的验证

- **没在 Unity 编辑器里按过 Play**（交付过程中没有启动过编辑器）。
  也就是说：编译层面和规则层面都有实测证据，但"uGUI 实际渲染出来的样子"没有实测过 ——
  布局细节（间距、字号、窗口高度）可能需要你第一次运行时微调。
- 逻辑曾与网页版 `tools/smoke-test.js`（271 项断言）对照，但两边是各自独立实现的，
  不是同一份测试代码。

