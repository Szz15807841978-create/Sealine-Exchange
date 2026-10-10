# DiveLoop 随机地形

地形由 3D 模型和真实 BoxCollider 组成，角色在 XY 平面游动：

- `DiveTerrain3DView` 用 Seed 选择模型，生成底部海床和少量左右岩台。
- 每个模型的父对象都有一个根据旋转后模型包围盒生成的 BoxCollider；旧模型自带的 Collider 关闭，避免重复碰撞。
- 玩家和敌人直接查询这些 3D Collider。冲刺使用分步检测。没有网格碰撞或网格地形。
- 保留 Prefab 已保存的轴向修正旋转。模块表的 adjustments 中可以分别调整 rotationOffset、colliderSize 和 colliderOffset。
- 拱门暂不参与随机生成，避免单个箱体挡住拱门洞口。当前模块已够搭建简单布局。
- 相同 Seed 的地图完全一致，便于复现问题和设计关卡。

## Play 模式快捷键

- `F6`：随机一个新 Seed，立即生成另一张地图。
- `F7`：使用当前 Seed 重开，应得到完全相同的地图。

## 增加模块

1. 把新的 3D Prefab 放进 `Assets/Art/地形文件`。
2. 执行菜单 `鲸落/搜打撤/重建 3D 地形模块表`。
3. 在 `Assets/Resources/WhaleFall/DiveTerrainCatalog.asset` 中调整 platformCount（岩台数量）、platformWidth（宽度）和各模块的 adjustments。

按文件名建立专用引用，不依赖 Prefab 内部保留的英文模型名：

| 模块 | 用途 | 当前生成 |
|---|---|---|
| 边界-平台 | 海床与横向岩台 | 是 |
| 边界-崖壁 | 左右边界岩壁 | 是 |
| 海底-缓坡 | 海床高度过渡 | 已收录，预留 |
| 海底-阶梯 | 分层海床 | 已收录，预留 |
| 海底-柱子 | 局部障碍与地标 | 已收录，预留 |
| 海底-拱桥 | 可穿越拱洞 | 已收录，等待分段碰撞箱 |
| abstract_rock_formations | 装饰岩群 | 已收录，预留 |

第一阶段只用平台与崖壁搭建简单地形。其余模块不混入普通平台池；尤其拱桥不能用一个大箱体覆盖整个洞口。
