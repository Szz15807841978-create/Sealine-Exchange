# 2026-10-10 仓库整理记录

## 保持原位置

- `Unity/Assets` 目录结构、场景、美术资源及全部 `.meta` 保持原样；六份编辑器维护脚本仅调整本地素材工作目录引用。
- `Unity/ProjectSettings`：保留已有 Force Text、Visible Meta Files 和 Build Settings。
- `index.html`、`player.html`：文件位置与内容保持不变。
- `Unity/README_Unity.md`：保留完整工程历史与场景维护说明。

## 新的资料位置

| 原位置 | 新位置 |
| --- | --- |
| 根目录 README.md | docs/gameplay.md；根目录改为协作入口说明 |
| 说明.txt | docs/history/original-requirements.txt |
| 根目录两张参考图片 | docs/references/ |
| _downloads/ | _local/downloads/ |
| _underwater_packages/ | _local/packages/ |
| Unity_*Backup*、Unity_Temp_StripControls_20261003 | _local/archive/ 下同名目录 |
| UnityMapBuild/ | _local/archive/UnityMapBuild/ |
| DivePoint_同事交付_20261005 目录及 zip | _local/archive/ 下同名目录及文件 |
| tools/backups/ | _local/archive/web-backups/ |
| 根目录日志 | _local/logs/ |
| 一次性 cleanup 脚本与结果 CSV | _local/archive/ |

这些归档仅保存在本机，Git 忽略它们。旧交付工程中的历史绝对路径不作为可移植协作配置维护。

## 测试与依赖

测试需要的两份旧 HTML 已复制到 `tools/fixtures/`，四个回归脚本已更新引用；其余备份仅归档。Python 与 Unity 编辑器资源维护脚本已将 `_underwater_packages` 引用改为 `_local/packages`。

Tripo Bridge 从外部绝对路径改为随仓库保存的相对路径包；原 manifest 和 lock 文件保存在 `_local/` 供本机追溯。

Unity/tools 下生成报告、截图、请求标记和构建产物保留本机且不入库。仅工具脚本与 RuleCheck 源码入库。
