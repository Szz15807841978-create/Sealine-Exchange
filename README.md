# Sealine Exchange · 捕捞交易所

Unity 游戏工程与单文件网页玩法 Demo。协作仓库：https://github.com/Szz15807841978-create/Sealine-Exchange

## 从这里开始

- **Unity 开发**：用 Unity Hub 打开 `Unity/`，编辑器版本为 **2022.3.62f3c1**。先安装 Git LFS 并拉取资源，详见 [协作指南](CONTRIBUTING.md)。
- **网页体验**：双击根目录的 `player.html`；数值实验版为 `index.html`。两个文件保留原位置和各自存档。
- **项目实现说明**：[Unity 工程说明](Unity/README_Unity.md)；[网页玩法与模型记录](docs/gameplay.md)。

## 目录

```text
Sealine-Exchange/
├─ Unity/                 Unity 主工程（在 Unity Hub 中打开这一层）
│  ├─ Assets/             场景、脚本、模型、材质、贴图与 .meta
│  ├─ Packages/           Unity 包依赖及锁定文件
│  ├─ LocalPackages/      随仓库分发的本地插件
│  ├─ ProjectSettings/    团队共享的编辑器和构建设置
│  └─ tools/              Unity 校验脚本和规则测试源文件
├─ docs/                  玩法、历史需求、参考图与整理记录
├─ tools/                 网页回归测试和资源处理工具
│  └─ fixtures/           回归测试依赖的两份历史网页样本
├─ player.html            玩家版网页入口
├─ index.html             数值实验版网页入口
├─ package.json           无第三方依赖的引擎测试命令
├─ CONTRIBUTING.md        克隆、分支、提交与协作规范
├─ .gitignore             本地缓存、下载、凭据与生成物排除规则
├─ .gitattributes          Git LFS 和文本换行规则
└─ _local/                仅本机：旧交付包、历史备份、下载包与日志
```

`_local/`、Unity 缓存和测试输出不上传。旧资料保留在本机，详见 [整理记录](docs/repository-layout.md)。

## 快速校验

安装 Node.js 18 或更新版本，在仓库根目录运行（这些引擎测试不需要 npm install）：

```powershell
npm test
npm run test:model
```

浏览器交互校验另需 Playwright 与 Edge；Unity 校验脚本需要本机 Unity 编辑器，具体见协作指南。

## 协作约定

每个任务创建独立分支，推送后通过 Pull Request 合并。场景、Prefab 和大型美术资源先约定负责人，避免同时修改。移动 Unity 资源时使用 Unity 编辑器，并将对应 `.meta` 一起提交。

第三方资源和插件保留原有许可证与使用限制；本仓库未额外授予这些素材的再分发权。
