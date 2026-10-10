# 本地包依赖

`Unity/Packages/manifest.json` 与 `packages-lock.json` 原来引用：

```text
file:E:/unity资源/Tripo3d_Unity_Bridge
```

为让克隆项目的成员获得同一份插件，已将原有 1.0.14 包逐文件复制到 `Unity/LocalPackages/com.tripo3d.unitybridge`，并改为相对于 Packages 目录的路径：

```text
file:../LocalPackages/com.tripo3d.unitybridge
```

原包未修改。上游地址来自包自身的 package.json：
https://github.com/tripo3d/Tripo3D-Unity-Bridge

此处固定的是本机现有包快照，未声明它对应某个已验证的上游提交。今后升级时请一起审查包内容和版本变化。
