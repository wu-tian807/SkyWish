# 本地开发

项目目录：`D:\Resources\workspace\city-builder`。
已检测游戏：`D:\SteamLibrary\steamapps\common\Cities Skylines II`。
首次准备时的 Steam build：`25127643`。没有把游戏 DLL 复制进自有模组输出。

## 工具与编译

```powershell
# 已在本机完成；新环境才需要重新准备。
.\scripts\Bootstrap-DotNet.ps1
npm ci --ignore-scripts

# 不自动部署到游戏。
.\scripts\Build-Mod.ps1
# 其他安装位置：
.\scripts\Build-Mod.ps1 -GamePath 'E:\SteamLibrary\steamapps\common\Cities Skylines II'

.\scripts\Test.ps1
npm run doctor
```

便携 SDK 位于 `.tools/dotnet/`，下载元数据和 SHA512 记录位于 `.tools/dotnet-sdk.json`。
SDK 仅用于编译；游戏模块目标仍为 `net472`，使用游戏自己的运行时和引用。
Node 依赖由 `package-lock.json` 固定。首次恢复包需要联网。

`Test.ps1` 编译模组和独立 C# 通信测试程序，再运行 Node/MCP 检查和源码完整性检查。
限制了 Windows 用户身份的执行环境可能无法访问只允许当前用户的命名管道；
此时应在同一 Windows 用户的普通终端验证，不通过放宽游戏管道权限来绕过。

## 加载到游戏与验收

已安装本地模组，并创建本地测试存档“克尔曼”（无垠平原、无限资金、全部解锁）。当前验收结果见 `VALIDATION.md`。更新模组前先退出游戏：

```powershell
.\scripts\Install-Mod.ps1
```

通过 Steam / Paradox 启动器正常启动 CS2，加载用于开发的城市。检查模组旁的 `CityDirector.bootstrap.log` 和游戏日志中的加载记录，再运行 `npm run doctor`。
游戏右上角的“导入资产库”是本地模组入口；Paradox 播放集显示的下载模组数不能单独判断本地开发 DLL 是否加载。已完成测试长椅导入，以及完整重启后载入测试城市、检查注册表的验证，另存的测试存档为“克尔曼 2”。

本机曾出现工具文件视图与游戏实际文件视图不一致：安装脚本回读成功，但资源管理器内没有对应目录。因此不能只以脚本回读判断部署成功。本次实际通过资源管理器将 `artifacts/deploy-runtime-fix/CityDirector/` 复制到游戏用户目录的 `Mods/CityDirector/`，再以独立启动日志及实时 MCP 查询确认加载。该目录包含 DLL 和旁边的资产库配置；更新前退出游戏，文件覆盖对话框需要在其自己的窗口完成。

0.2 版本支持独立新资产的导入；具体限制、用户控制与持久化设计见 `ASSET_WORKFLOW.md`。
0.2.1 安装还必须包含 `cleanup/CityDirector.Cleanup.exe`。它是无额外运行库依赖的 .NET Framework 程序，只在用户确认删除后隐藏启动，等待游戏退出再清理。单独替换模组 DLL 不足以启用删除功能。`Test.ps1` 会编译共享删除逻辑的测试程序并运行真实文件边界测试。
编译通过、桌面 .NET 通信通过，都不能替代这个检查。

如果启动失败，先查看日志；不要把测试程序旁的 Newtonsoft.Json.dll 复制进游戏。
游戏内使用游戏自带版本，桌面测试使用 NuGet 签名版本，两者运行环境不同。

## Codex 接入

官方说明支持命令启动的 STDIO MCP，以及配置中的 `command`、`args` 和 `cwd`：
[OpenAI MCP 文档](https://developers.openai.com/codex/mcp)。

可合并 [配置示例](../config/codex.example.toml) 到用户或项目的 Codex 配置，调整路径后重启客户端。
本机已通过 `codex mcp add` 注册用户级 `city_director`。当前聊天不会因此自动获得新工具；可用 `node scripts/mcp-call.mjs director_status` 验证真实 STDIO MCP 客户端连接。

也可以在装有 Codex CLI 的环境运行：

```powershell
codex mcp add city_director -- node 'D:\Resources\workspace\city-builder\src\harness\server.mjs'
```

工具服务器使用官方 MCP SDK，通过 STDIO 启动，不需要 OpenAI API key。
日志不能写到 STDOUT，以免破坏协议。游戏桥接通过 `\\.\pipe\city-director-v1` 连接，
游戏和 MCP 服务应在同一 Windows 用户下运行，首版只支持一个游戏实例。

| MCP 工具 | 当前作用 |
|---|---|
| `director_status` | 模组在线与实际能力；不代表已经加载城市 |
| `director_game_state` | 游戏模式、城市名、加载状态、暂停与速度 |
| `director_find_assets` | 搜索 Prefab 名称/类型；不保证都可直接放置 |
| `director_reference_sources` | 本地参考源码清单；游戏关闭时也可用 |
| `director_validate_asset` / `director_stage_asset` | 校验并保留不可覆盖的模型包、来源与预览 |
| `director_import_asset` / `director_import_status` | 提交独立新资产导入并查询持久化任务记录 |
| `director_asset_library` / `director_archive_asset` | 查询资产库、归档或恢复；不删除原生资源 |
| `director_menu_tree` | 查询当前城市已注册的原生菜单与分类，包括其他模组的已加载分类 |
| `director_menu_assets` | 按 categoryId 分页查询分类注册条目，支持名称搜索 |
| `director_assign_asset_category` | 为本模组管理的导入资产设置已有分类、标题、说明和排序；热刷新并持久化 |

## 参考源码复现

```powershell
.\scripts\Fetch-Sources.ps1
npm run sources:verify
```

已有目录不会被重置或覆盖。复现脚本从 GitHub 获取固定提交，不是浮动最新分支。
`sources:verify` 检查提交、远程、工作树、Git 对象完整性和许可证文件哈希。
`snapshot-sources.mjs` 只用于有意更新参考版本后重新记录清单，日常验证不要用它替代 verify。

`third_party/`、`.tools/`、`.cache/`、`artifacts/` 和 `node_modules/` 不进入主项目版本控制。
