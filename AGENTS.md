# 项目知识库

## 概览

PlateUp 多模组仓库；每个模组在 `mods/<ModId>/` 中独立维护源码、说明、工坊素材和发布内容。
现有中转模组使用 C#、Harmony、Unity 和 LiteNetLib；具体网络与 UI 约束见模组内的 AGENTS。

## 结构

```text
.
├── MLJ.PlateUp.Mods.sln       # 所有已加入的 C# 项目
├── NuGet.Config
├── packages/                 # 本地 NuGet 包源，包文件不入库
├── mods/
│   └── PlateUpRelay/
│       ├── src/
│       ├── assets/
│       ├── docs/
│       ├── scripts/
│       ├── deploy/
│       ├── licenses/
│       ├── workshop/
│       ├── dist/             # 本地产物，不入库
│       └── README.md
└── README.md
```

## 去哪里找

| 任务 | 位置 | 说明 |
| --- | --- | --- |
| 仓库导航与新增模组 | `README.md` | 目录边界、加入解决方案 |
| 修改中转模组 | `mods/PlateUpRelay/AGENTS.md` | 协议、服务器与发布约束 |
| 修改中转客户端 | `mods/PlateUpRelay/src/Mod/AGENTS.md` | UI、Steam 生命周期与配置 |
| 调整包源 | `NuGet.Config`、`packages/README.md` | 本地源与 nuget.org |
| 统一构建 | `MLJ.PlateUp.Mods.sln` | 项目按模组分组 |

## 约定

- 每个模组独占 `mods/<ModId>/`；脚本、工坊物品 ID、版本和依赖保留在所属模组内。
- 模组 README 中的路径相对于模组目录；根 README 中的路径相对于仓库根。
- 新模组按实际需要建立目录；内容模组不继承中转模组专用的 Server、Shared、部署服务或群服配置。
- `src/Shared` 是所属模组内部的共享代码，不代表整个仓库的公共运行库。
- `NuGet.Config` 清除继承包源，声明根目录 `packages` 与 nuget.org；本地源路径相对该配置文件。
- 手写说明与行为注释主要用简体中文；游戏文案的语言支持在各模组内维护。

## 禁止事项

- 不随模组分发游戏本体程序集；游戏引用应为 `Private=false`。
- 不将一个模组的工坊物品 ID 或上传目录复用于另一个模组。
- 不把 `dist`、`bin`、`obj` 或本地 NuGet 包提交为源码；分发依赖时保留其许可。

## 命令

在仓库根目录使用 Windows .NET SDK：

```powershell
dotnet build .\MLJ.PlateUp.Mods.sln -c Release
```

各模组的独立构建、发布和部署命令见其 README；没有仓库级格式化、lint 或 CI 配置。

## 注意

- 项目目录迁移后须重新还原依赖；旧 `obj` 中的绝对路径不能作为新路径的证据。
- 源码构建不会自动更新工坊上传内容、游戏已安装副本或线上服务。
- 公开仓库不包含游戏程序集；中转项目的 `GameDir` 和 `HarmonyPath` 可由 MSBuild 属性覆盖。
- 工坊外部政策与账号发布资格须在实际发布时核实；不以仓库说明代替平台状态。
