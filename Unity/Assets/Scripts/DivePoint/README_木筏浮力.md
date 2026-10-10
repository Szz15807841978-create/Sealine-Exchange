# 木筏 ↔ 海面交互（受控浮力 · 第一阶段）

让木筏真正随浪起伏，而不是靠一条正弦假装在晃。

## 为什么之前不能起伏

木筏和海面**不在同一个世界空间里**，这是当前侧视玩法的结构决定的：

| | 位置 | 由谁渲染 | 去哪 |
| --- | --- | --- | --- |
| 海面 | `Crest海洋_参数可编辑` @ (2000, 0, 0) | `海洋与天空摄像机`（透视，depth -20） | `RealisticOceanSky.renderTexture` |
| 木筏 | `RaftRoot` @ (≈1000, -2.23, 0)，layer 30 | `3D木筏展示摄像机`（正交，depth -10） | `RaftImported3D_Render` |

两张 RenderTexture 各自贴到画布上的 RawImage（`真实海洋与天空_渲染画面` / `3D木筏实时画面`）叠在一起，
看着是一体的，物理上相隔 1000 米、各在各的图层里。所以以前只能给 UI 上的木筏图加一条 `Mathf.Sin` 假起伏。

## 现在怎么做

核心是加了一个**海面采样锚点**（`Crest海洋_参数可编辑/海面采样锚点`）：它站在真实海面上，
木筏底部的 5 个采样点的**水平偏移**换算到锚点坐标系里去查询 Crest，于是「木筏该怎么动」由「锚点处的浪」决定，
两个隔离空间从此有了对应关系。

```
每帧（LateUpdate）：
  5 个采样点世界坐标 = 锚点位置 + 采样点相对木筏原点的 XZ 偏移
  一次批量查询 Crest：ICollProvider.Query(5 个点 → 5 个水面高度)
  最小二乘拟合平面 y = a + b·x + c·z
      升沉   = a - seaLevel - HeaveBias      （整体升降）
      俯仰   = atan(b)                        （前后，绕木筏 Z 轴）
      横滚   = atan(c)                        （左右，绕木筏 X 轴）
  三个量各自按半衰期平滑 + 限幅，叠加到木筏的静止姿态上
```

### 为什么用平面拟合，而不是相邻两点相减

5 个点两两相减会得到互相矛盾的角度，噪声被放大一个量级。拟合平面用到全部点，
单点抖动只占 1/5 权重。`tools/run-raftcheck.ps1` 里有一条断言专门量化这件事：
给 5 个点各加 ±5 cm 噪声跑 200 次，俯仰误差始终 < 1°、横滚 < 4°。

> 横滚天生比俯仰吵 5—6 倍，因为甲板**沿 X 长 12.6 米、沿 Z 只有 2 米出头**，
> 横滚是用很短的一条基线定出来的。这不是 bug，是窄船体的物理事实；
> 代码侧靠 `TiltHalfLife`（默认 0.55 s）平滑 + `MaxRollDeg`（默认 6°）限幅兜住。

### 为什么采样用静止姿态（`SampleInRestFrame`）

若按船体**当前**姿态摆采样点，会出现「倾斜 → 采样点跟着倾斜 → 测出更大倾斜」的反馈回路，
在窄船体上很容易自激。第一阶段先切断这个回路，保证「自然起伏 + 轻微倾斜、绝不翻覆」。
进入真正的搜打撤水域、换成完整刚体浮力时，把 `SampleInRestFrame` 关掉即可。

## 场景里的对象

| 对象 | 位置 | 作用 |
| --- | --- | --- |
| `RaftRoot` | `3D木筏与装置_全部可编辑` 下 | **承载位姿的枢轴**。甲板 + 全部固定装置都是它的子对象，所以整体自动跟随 |
| `浮力采样点_中心/前/后/左/右` | `RaftRoot` 下，贴甲板底面 | 5 个浮力采样点，可在 Scene 视图里直接拖 |
| `海面采样锚点` | `Crest海洋_参数可编辑` 下 | 隔离空间 → 真实海面的映射锚点 |
| `RaftBuoyancy` | 挂在 `RaftRoot` 上 | 采样 + 解算 + 摆位姿 |
| `RaftDeckRider` | 挂在 `下水点画布` 上 | 让 UI 上的玩家跟着倾斜的甲板走 |
| `AnimatedWaves_GamePhysics.asset` | `Art/Environment/RealisticOceanSky/` | 补上原本缺失的 Animated Waves 设置，Collision Source = ComputeShaderQueries |

`RaftWeatherController.DrivenByOcean` 勾上后会关掉原来的正弦假起伏，避免两种起伏叠加。

## 固定装置 vs 可掉落物品

- **固定装置**（木箱、石炉、蒸馏锅、木桶、加工器、工作台）：都在 `RaftRoot` 下，跟着甲板走，**不需要各自处理**。
  箱盖动画（`Chest3DView` 转 `箱盖转轴_后侧合页` 的**本地**旋转）和所有 UI 仍然是独立的，不受浪动影响。
- **可掉落物品**：用 `BuoyantObject`（独立刚体 + 碰撞体 + 多点浮力，受力制，能滚能滑能漂）。
  当前侧视玩法里还没有这类物品，所以**先备好没装**；进搜打撤水域后给物品预制体挂上、配 Collider 和 Rigidbody 即可。
  注意：物品若也在 layer 30 的隔离世界里，必须给它填 `OceanProbe`（和木筏共用同一个 `海面采样锚点`）。
- 第二阶段要换成完整刚体浮力时，把木筏也改成 `BuoyantObject` 路线即可，`RaftBuoyancySolver` 的 `SubmergedFraction` 已经备好。

## 玩家为什么不会脱离甲板

玩家在画布上是 RectTransform，木筏是另一张 RT 贴图，本来毫无关系。`RaftDeckRider` 只接管两件事：

1. **竖直跟随**：算出甲板在玩家所在甲板 x 处的高度，取「当前姿态 vs 静止姿态」的世界高度差，换算成 UI 像素加到 y 上。
2. **随板倾斜**：幅度取 `RaftBuoyancy.PitchDeg`。

横向位置**完全不动** —— 玩家的 x 仍是 `DivePointController` 的玩法坐标（左右限位、和木箱的距离判定全部不变）。
能这样分工是因为 `DivePointController.MovePlayer` 只写 `anchoredPosition.x`、保留当前 y，
所以 U 层的 y 交给本组件独占，两者不打架。

UI x ↔ 甲板 x 的换算**不靠手量常数**，靠木箱标定：木箱的隐形 2D 标记（`木箱实体_ITM_DEV_CHEST_01`）
和木箱 3D 实体（`交互木箱3D`）指的是同一个箱子，用它们定出比例和原点。
安装器还会拿**石炉**当第二参照物反查符号，把「UI +x 到底对应甲板 +X 还是 -X」量出来而不是猜：

```
石炉甲板 x=0.100，2D 标记实测 x=5.0；以木箱(x=-1.703/UI -170)为原点预测：
  +1 → 18.6（差 13.6）    -1 → -358.6（差 363.6）      → 取 +1
```

预测残差 0—20 px（画布上木筏宽 1260 px），这个误差来自 2D 标记是**设计值**、3D 模型是照着手调位置摆的，
不是换算公式错。换算比例 104.6 px/单位与摄像机正交视口 (`548 / (2×2.62)`) 算出来的值吻合到 4 px 以内。

## 想调手感改哪里

全在 `RaftRoot` 上的 `RaftBuoyancy`（和 `Sim.cs`/`Cfg.cs` 一样的思路：规则集中一处）：

```csharp
MinSpatialLength = 12.6f   // 船长。越大越忽略细小波长，运动越稳
SampleInRestFrame = true   // 无反馈采样（第一阶段建议保持 true）
HeaveHalfLife   = 0.35f    // 升沉跟随半衰期（秒），越大越迟钝
TiltHalfLife    = 0.55f    // 倾斜跟随半衰期（秒）
MaxPitchDeg     = 5f       // 俯仰限幅 —— 受控浮力靠它保证不翻覆
MaxRollDeg      = 6f       // 横滚限幅
HeaveBias       = 0f       // 正值整体抬高，微调吃水
PitchSign/RollSign = 1f    // 装好后若方向相反，改成 -1
```

极简做法：想让木筏更"重"，把两个半衰期调大；想更颠，调小并放宽限幅。

## 验收

```powershell
# 浮力解算的 54 项断言（不需要 Unity）
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-raftcheck.ps1

# 对着真实 Unity/Crest 程序集编译本功能（不开编辑器也不怕写坏工程）
powershell -NoProfile -ExecutionPolicy Bypass -File tools\run-compilecheck.ps1
```

安装/重装场景接线：

```
Unity 菜单：鲸落 → 木筏 → 安装木筏浮力（受控）
```

或放一个 `Assets/Editor/RaftBuoyancy.request`，编辑器下次域重载时自动执行。
执行结果写在工程根目录的 `RaftBuoyancySetup.success.txt`（失败则 `...failed.txt`，含逐场景原因），
并用木筏摄像机实渲两张对比图到 `BuoyancyPreview/<场景名>_静止.png` 与 `_浪中.png`。

## 已知边界

- **只在 Play 模式生效**（`RaftBuoyancy.LateUpdate` 里有 `Application.isPlaying` 判断）。
  编辑模式不驱动，避免重蹈 Crest `OceanChunkRenderer` 在编辑模式下刷异常的覆辙。
- Crest 的高度查询是**异步**的：本帧投递、下一帧取回，所以木筏的位置比真实海面晚一帧。
  头一两帧取不到结果属正常，代码会等 20 帧才告警。
- 侧视阶段木筏走的是「受控浮力」（直接摆位姿），不是真实受力。**不会翻覆**，也**不会横向漂移**。
