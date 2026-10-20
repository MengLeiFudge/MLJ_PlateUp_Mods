# MLJ PlateUp Mods

PlateUp 模组集合。每个模组在 `mods/<ModId>/` 下独立维护源码、使用说明、工坊素材和发布内容。

## 模组

| 模组 | 用途 | 使用说明 |
| --- | --- | --- |
| [PlateUpRelay](mods/PlateUpRelay/) | 保留 Steam 邀请，通过自建服务器中转对局数据 | [安装、配置、部署与构建](mods/PlateUpRelay/README.md) |

中转模组的群友安装入口为 [PlateUpRelay一键安装.bat](mods/PlateUpRelay/scripts/PlateUpRelay一键安装.bat)。

## 目录

```text
MLJ_PlateUp_Mods/
├── MLJ.PlateUp.Mods.sln       # C# 项目统一入口
├── NuGet.Config
├── packages/                 # 本地 NuGet 包源
└── mods/
    └── PlateUpRelay/
        ├── src/              # Mod、Server、Shared
        ├── assets/           # 封面和素材制作脚本
        ├── docs/             # 中英文工坊说明
        ├── scripts/          # 分发给玩家的安装脚本
        ├── deploy/           # 服务部署配置
        ├── licenses/         # 第三方许可
        ├── workshop/         # 工坊上传目录与物品元数据
        ├── dist/             # 本地发布包，不纳入 Git
        └── README.md
```

`bin`、`obj`、`dist`、本地 NuGet 包和工坊分发 DLL 均不纳入 Git。下载源码后按对应模组的说明构建；游戏程序集由本机游戏安装提供。

## 构建

在仓库根目录使用 Windows .NET SDK：

```powershell
dotnet build .\MLJ.PlateUp.Mods.sln -c Release
```

仅构建中转客户端：

```powershell
dotnet build .\mods\PlateUpRelay\src\Mod\PlateUpRelay.csproj -c Release
```

中转服务可独立发布，无需游戏程序集：

```powershell
dotnet publish .\mods\PlateUpRelay\src\Server\PlateUpRelay.Server.csproj -c Release -r linux-x64 --self-contained true -o .\mods\PlateUpRelay\dist\server
```

客户端项目的 `GameDir` 与 `HarmonyPath` 默认指向本机 Windows 安装位置，其他电脑可通过 MSBuild 属性覆盖。包源由根目录 `NuGet.Config` 统一配置。

## 添加内容模组

1. 新建 `mods/<ModId>/`，将客户端项目放入 `src/Mod/`，程序集名称与工坊身份独立设置。
2. 在该模组的 `README.md` 记录用途、前置、安装和构建方法；素材放入 `assets/`，工坊文案放入 `docs/`。
3. 需要发布时建立独立的 `workshop/content/`。该模组的 `plateup_mod_metadata.json` 位于 `workshop/` 下，不沿用其他模组的物品 ID。
4. 将项目加入根解决方案，并在上方模组表中添加入口。例如，将 `<ModId>` 替换为实际名称后执行：

```powershell
dotnet sln .\MLJ.PlateUp.Mods.sln add .\mods\<ModId>\src\Mod\<ModId>.csproj --solution-folder <ModId>
```

按模组需要建立 `scripts`、`licenses` 等目录。内容模组只添加自身需要的前置；`PlateUpRelay` 的 `Server`、`Shared` 和 `deploy` 属于中转功能，不需要复制到内容模组中。

## 中转模组文件位置

- 工坊上传器的 **Mod Directory**：`mods/PlateUpRelay/workshop`。
- 工坊封面：`mods/PlateUpRelay/assets/工坊封面.png`。
- 客户端 ZIP：`mods/PlateUpRelay/dist/PlateUpRelay.zip`。
- Linux 服务端发布包：`mods/PlateUpRelay/dist/server`。

目录整理只影响仓库文件位置；游戏中的 `Mods/PlateUpRelay`、服务器的 `/opt/plateup-relay` 和现有工坊物品 ID 保持原值。
