# 协作指南

## 首次克隆

安装 Git、Git LFS 和 Unity Hub，然后运行：

```powershell
git lfs install
git clone https://github.com/Szz15807841978-create/Sealine-Exchange.git
cd Sealine-Exchange
git lfs pull
```

请使用 Git 克隆，以确保模型和贴图的 LFS 内容被下载。Unity Hub 添加仓库内的 `Unity` 文件夹，使用 `Unity/ProjectSettings/ProjectVersion.txt` 指定的 **2022.3.62f3c1**。首次导入素材需要时间。

常用场景：

- 玩家捕捞：`Assets/Scenes/Main.unity`
- 木筏入口：`Assets/Scenes/Current/DivePointRealisticOceanSky_Unified.unity`
- 水下探索：`Assets/Scenes/UnderwaterExplore3D.unity`

现有场景已保存手工布局。不要为了预览项目运行场景生成器、批量资源导入器或旧版全量测试菜单；这些工具可能重建场景。具体限制见 `Unity/README_Unity.md`。

## 每次开始任务

保存 Unity 场景，确认当前修改已提交或暂存，再切换分支：

```powershell
git switch main
git pull --ff-only
git switch -c feature/your-task
```

修改后检查文件范围，再提交需要的文件：

```powershell
git status
git add <本次修改的文件或目录>
git commit -m "描述本次修改"
git push -u origin feature/your-task
```

首次提交时，按提示配置你自己的 `user.name` 与 `user.email`；可以使用 GitHub 账户提供的隐私邮箱。不要复制其他人的身份。

在 GitHub 创建 Pull Request，请另一人查看和运行必要验证后合并。涉及同一场景、Prefab、模型或贴图时先沟通分工；遇到冲突不要直接覆盖另一人的版本。

## Unity 与大文件

- 所有资源对应的 `.meta` 都要提交。资源移动、重命名优先在 Unity Project 窗口操作。
- 工程已使用 Force Text 和 Visible Meta Files。不要随意改变序列化模式或升级 Unity。
- 模型、贴图和音频等使用 Git LFS；文本场景、Prefab 和脚本保留为可比较的文本。
- `Library`、`Temp`、`Logs`、`UserSettings` 和工具输出由每台电脑自行生成。
- Tripo Bridge 1.0.14 已放入 `Unity/LocalPackages/com.tripo3d.unitybridge`，依赖采用相对路径，不再要求存在作者的 E 盘目录。来源说明见 `docs/local-dependencies.md`。
- 素材体积较大，首次推送与每位成员首次拉取前检查仓库所有者的 Git LFS 配额。不要为了试传反复提交同一份大型二进制文件。

## 验证

根目录执行：

```powershell
npm test
npm run test:model
```

`npm test` 使用 Node 内置模块，覆盖现有实验版、玩家版、存档迁移和风险模型回归。`tools/fixtures` 中的旧引擎用于迁移对照，不能按无用备份删除。

浏览器交互测试为 `node tools/player-check.js` 和 `node tools/browser-check.js`，另需 Playwright 与 Microsoft Edge。它们生成的截图不入库。

Unity 工具位于 `Unity/tools/`。部分脚本默认编辑器位置为作者机器的 E 盘，需要用 `-UnityEditor` 参数指定你自己的编辑器目录；部分集成检查还依赖先前生成的程序集，首次克隆应先用 Unity 完成导入和编译。不要把运行脚本成功等同于所有场景已完成实际 Play 验证。

## 资源导入工具

`tools/import_*.py`、`tools/stage_*.py` 是美术资源维护工具，普通克隆和运行无需执行。原始下载包放在 `_local/packages`，不上传 GitHub。部分扩展导入脚本还引用作者本地的素材压缩包，需要按使用者电脑调整路径。

## 邀请成员

仓库所有者在 GitHub 仓库设置中邀请朋友作为协作者；私有仓库的受邀成员接受邀请后才能克隆和推送。仓库可见性及权限由所有者按团队需要管理。
