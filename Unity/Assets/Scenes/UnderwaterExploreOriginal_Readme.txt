鲸落 · 原场景下潜演示

打开 UnderwaterExploreOriginal.unity，点击 Play。
WASD / 方向键：上下左右游动；Shift：加速；R：返回浅水起点。
人物沿固定 YZ 平面游动，X 锁定。场景、角色、镜头和碰撞均为 3D。
沿画面右下方的原海床坡谷可进入约 40 米深的区域。

基础：Davis3D/Ocean Environment Pack 的 Scenes/Demonstration.unity。
直接复制完整演示场景，保留原地形、岩礁、植被、灯光、鱼群轨迹、水母、材质与粒子布置。
原场景文件没有修改。新增角色、侧视跟随相机、海床网格碰撞；仅调整侧视距离对应的雾密度。
原包中的 149 组海葵有失效的 LOD 网格引用，在副本中重新连接了 596 处网格引用。
副本移除了两个资源包未附依赖的旧后处理脚本组件；未安装或切换渲染管线。

验证：Unity Play Mode 下五个连续航点均到达；锁定平面、跟随镜头和海床碰撞通过。
验证记录和画面在工程外 ../_underwater_packages/OceanReview/。

此前的 UnderwaterExplore.unity 是简化拼装草稿；本次交付请使用带 Original 的场景。
