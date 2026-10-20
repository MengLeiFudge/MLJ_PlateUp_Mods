# PlateUp 游戏中转模组

本模组位于仓库的 `mods/PlateUpRelay/`。下文源码、素材和发布文件路径均相对此目录；仓库总览见[根 README](../../README.md)。

在游戏“设置 → 游戏中转”中输入中转服务器 IP，让 PlateUp 的对局数据经过自己的服务器转发。右下角版本文字上方显示 `Server … ms, Room … ms`。保留 Steam 邀请、房间管理及房主执行游戏逻辑的方式；不修改游戏或其他多人模组的人数规则。

**使用前提：服务器须部署并运行配套中转程序 `PlateUpRelay.Server`，并放行其监听的 UDP 端口（默认 27841）。仅开放端口，或填写运行其他服务的服务器 IP，无法使用本模组中转。** 配套程序源码位于 `src/Server`，构建方式见下方“源码与构建”。房主和其他玩家均须安装模组，并填写同一中转服务的 IP 和端口。

版本号见 `src/Mod/PlateUpRelay.csproj` 的 `Version`，运行时可在 `Player.log` 中搜索 `[PlateUpRelay] v` 核对已加载版本。连接期间保留 Steam 入房流程，等待身份确认和中转就绪后发送对局数据；无法完成连接时沿用游戏自身的超时处理。支持房间延迟测量的客户端要求配套协议 v2 服务；新服务端仍接受 v1 客户端。升级时先更新服务端，再让同一房间的所有玩家统一更新模组。

## 群友一键安装（Windows）

先由管理员更新配套服务端并上传新版工坊文件，再将[scripts/PlateUpRelay一键安装.bat](scripts/PlateUpRelay一键安装.bat) 发给群友。脚本固定配置群服 `47.115.145.112`；公开模组自身没有默认服务器。

1. 安装 PlateUp，登录拥有游戏的 Steam 账号，并完全退出游戏。
2. 双击 BAT。脚本通过游戏自带的 Steam API 检查本模组及 Harmony：未订阅时自动订阅；已订阅、本地文件齐全且没有待更新内容时直接使用；文件缺失或需要更新时才下载并等待完成。不会要求输入 Steam 密码。
3. 脚本检查版本和必需文件，备份旧的本地模组及配置，再安装到游戏内层目录的 `Mods\PlateUpRelay`，并写入服务器地址。看到“安装完成”后再启动游戏。
4. 按原来的 Steam 方式邀请或加入。工坊副本保留订阅，Harmony 由工坊加载，中转模组由本地 Mods 加载。更新本地副本时重新运行脚本。

脚本自动读取 Steam 库位置，支持非默认盘符。旧文件保存在游戏内层目录的 `PlateUpRelay.Backups`，与 Mods 同盘且不在 Mods 目录内，便于失败时恢复。下载失败、工坊版本过旧或发现其他目录中的重复中转 DLL 时会明确停止；失败输出包含步骤、错误位置及相关目录，可将完整输出发给管理员定位。不会强制关闭游戏或删除其他模组。此脚本供群内当前使用场景安装，不适用于官方恢复工坊加载后同时保留两份副本的情形。

## 手动安装与更新

1. 退出游戏，保留 Harmony 前置模组（物品 ID 2898033283）。本模组不另外依赖 KitchenLib。
2. 将分发包 `content` 目录（或本模组的 `workshop/content`）中的 `PlateUpRelay.dll`、`LiteNetLib.dll` 和 `LiteNetLib-LICENSE.txt` 复制到游戏所在 Steam 库的 `steamapps\common\PlateUp\PlateUp\Mods\PlateUpRelay`，更新时替换同名文件。DLL 直接放在 `PlateUpRelay` 文件夹内。
3. 所有参与玩家使用相同版本，重启游戏，在主菜单或没有其他玩家的房间进入“设置 → 游戏中转”，设置相同服务器地址并启用。然后按原来的方式邀请或加入。

当前所核对的游戏加载器会跳过工坊 ID 大于 `3820868888` 的物品，本模组物品 ID 为 `3823021451`。本地 Mods 加载不受这一工坊 ID 检查影响。官方恢复新物品加载后，可改用下方订阅流程，同时移走本地副本，避免重复加载。

## 通过创意工坊使用

1. 发布后，在 Steam 创意工坊订阅本模组及 Harmony 前置模组（物品 ID 2898033283）。本模组不另外依赖 KitchenLib。
2. 等待 Steam 下载完成后重启游戏，让游戏加载新 DLL。
3. 在主菜单或没有其他玩家的房间中，打开“设置 → 游戏中转”，点击“中转地址”输入或粘贴地址，按回车确认。
4. 输入服务器管理员提供的公网 IPv4，点击“保存并启用”。默认 UDP 端口是 27841；使用其他端口时填写 `IP:端口`。请先确认该地址正在运行配套中转服务。
5. 所有参与玩家启用同一版本模组并填写同一服务器，然后按原来的 Steam 方式邀请、加入。
6. 右下角版本文字上方显示 `Server … ms, Room … ms`，字体和字号跟随原生版本文字。`Server` 是本机到服务器的往返延迟；`Room` 是经中转到房主的实测往返，未入房、尚无样本或样本过期时显示 `- ms`；本机房主显示 `Room 0 ms`。
7. “游戏中转”设置菜单保留两行详细连接状态。房间往返包含中转排队和房主处理时间，不等于帧率或操作响应时间。菜单跟随游戏语言，中英文均可用；原生输入框支持粘贴。无需新增 PreferenceSystem 前置。

“保存并启用”只保存地址并启用中转。中转连接需要 Steam 房间凭据；显示“未连接（已启用，等待 Steam 房间）”时，请按原来的 Steam 方式邀请或加入房间。服务器连接成功不代表房主中转已经就绪，两行状态分别显示。

服务器地址保存在游戏持久化目录的 `PlateUpRelay/server.txt`；Windows 通常为 `%USERPROFILE%\AppData\LocalLow\It's Happening\PlateUp\PlateUpRelay\server.txt`。没有该文件时兼容读取旧 PlayerPrefs，保存后以文件为准；空文件表示关闭中转。没有其他玩家时，可在设置菜单修改地址或点击“关闭中转，使用原联机”。返回游戏不会关闭中转，修改设置不会改变电脑的其他网络流量。

## 当前交付

`workshop` 是待上传内容目录，`dist/PlateUpRelay.zip` 是该目录的压缩副本。所有玩家使用同一套模组文件，无需独立账号配置或密钥文件。更新交付目录后，已安装的本地副本需要按上方步骤手动替换。

## 阿里云安全组

在这台 ECS 实例关联的安全组里放行入方向 UDP 27841，来源 IPv4 `0.0.0.0/0`。这是游戏中转程序的监听端口；安全组由控制台管理，本次尚未确认云端是否已放行。

如果客户端显示无法连接，先确认服务器上的 `PlateUpRelay.Server` 正在运行，再核对服务器 IP、UDP 端口、安全组、系统防火墙以及所有玩家是否开启同一个中转地址。提示“等待房主确认身份”时，房主也必须安装并开启同版本模组；Steam 登录和原有邀请通道需要可用。提示“连接被拒绝”时，另外核对是否已部署支持协议 v2 的服务端。跨平台 Photon 联机不在这个 Steam 版模组的接入范围内。

加入后返回自己房间时，查看 `%USERPROFILE%\AppData\LocalLow\It's Happening\PlateUp\Player.log`。搜索 `[PlateUpRelay]` 可区分 Steam 房间就绪、收到房主凭据、连接中转服务器和房主中转就绪几个阶段。`ClientRouterNotLive` 表示游戏活性超时；仅有 `Connected to relay` 不能证明已经连通房主。

## 数据路径

```text
房主与玩家加入原 Steam 房间
    → 模组通过 Steam 专用 P2P 通道确认成员并传递临时房间凭据
    → 每个游戏客户端连接自建 UDP 服务
    → 对局消息通过自建服务在房主与玩家之间转发
```

Steam 通道仅承担原有房间功能及中转初始化，完整游戏同步走自建服务器。服务使用 LiteNetLib 的 ReliableOrdered UDP 保留可靠、有序消息语义，并支持消息分片。服务器不读取游戏存档或模拟游戏逻辑；房主仍需保持在线。

同一房间通过临时凭据隔离，退出后清理连接。不使用玩家人数配置；消息大小和发送队列的资源边界用于防止异常连接无界占用内存。该通道不提供额外的端到端加密。

## 创意工坊发布内容

工坊介绍提供[中文版](docs/Steam工坊说明.txt)和[英文版](docs/Steam工坊说明.en.txt)，可复制到工坊对应语言的描述中；其中包含玩家连接步骤、管理员部署步骤和公开源码下载链接。

在上传器中，`workshop` 是 Mod Directory；工具固定读取其 `content` 子目录作为上传内容。目录结构为：

```text
workshop/
├── plateup_mod_metadata.json   # 保存现有工坊物品 ID
└── content/
    ├── PlateUpRelay.dll
    ├── LiteNetLib.dll
    └── LiteNetLib-LICENSE.txt
```

使用游戏提供的上传工具：

```text
D:\Steam\steamapps\common\PlateUp\PlateUp\PlateUp_Data\ModUploader.exe
```

在 **Update** 页的 **Mod Directory** 中选择：

```text
D:\project\other\MLJ_PlateUp_Mods\mods\PlateUpRelay\workshop
```

1. 确认 Mod ID 自动读取为 `3823021451`。更新现有条目，不需要再次 Create。
2. 勾选 **Update mod files**，Change Note 填写本次更新内容。该选项控制是否上传 `content` 中的 DLL。
3. 要设置工坊缩略图，勾选 **Preview Image** 旁的 **Update**，选择 `D:\project\other\MLJ_PlateUp_Mods\mods\PlateUpRelay\assets\工坊封面.png`。网页中添加截图与此预览图是不同的操作。
4. 已在网页填写的标题和描述可以保留；只有需要替换时才勾选对应的 Update。
5. 点击底部 **Update**，等待 **Mod updated successfully**。若提示需要接受工坊协议，按提示完成。
6. 刷新工坊页面，确认文件大小不再为零。随后在页面右侧“所有者控制”中找到“更改可见性”，选择“公开”或“仅限好友”。
7. 将 Harmony（2898033283）设为所需物品。

上传及可见性设置由你在 Steam 账号中操作；本次只整理本地上传包，没有代为发布。

同一版本选择本地目录或工坊订阅中的一种加载方式；官方解除工坊限制后，避免同时保留两份副本。

## 官方指南核对

依据：https://wiki.plateupgame.com/en/Modding/GettingStarted

- 代码模组为编译后的 DLL，按需引用游戏 `PlateUp_Data\Managed` 程序集。游戏本体程序集不随包分发。
- 设置菜单接入游戏 `OptionsMenu` 与原生 `TextInputView`；延迟文字随 `DisplayVersion` 创建，在版本组件销毁时清理。
- 本地 `Mods\模组名` 是指南支持的开发加载路径；正式使用可以通过 Steam 创意工坊订阅。
- 发布工具采用指南列出的 `PlateUp_Data\ModUploader.exe`。该页还写有 Workshop Beta 组资格要求，本次未验证你的账号发布资格，以工具实际提示为准。

指南给出的 Installation 子页链接当前返回 404。目录结构和上传选项另外核对了本机官方上传器的实际代码：`findContentDir` 固定使用 `Mod Directory/content`，`Update mod files` 默认未勾选；Create 页仅创建物品及本地元数据。

## 服务器维护

安装目录：`/opt/plateup-relay`。服务名：`plateup-relay.service`。默认 UDP 27841。Linux x64 self-contained 包已包含 .NET 运行时，不需要额外安装系统 dotnet。

```bash
# 查看运行状态和最近日志
sudo systemctl status plateup-relay --no-pager
sudo journalctl -u plateup-relay -n 50 --no-pager

# 临时停止或启动
sudo systemctl stop plateup-relay
sudo systemctl start plateup-relay

# 长期停用并取消开机启动
sudo systemctl disable --now plateup-relay
```

服务使用 systemd 临时低权限用户，不创建虚拟网卡，不使用 NAT 转发规则，也不修改服务器原有 Docker/1Panel 配置。`deploy/plateup-relay.service` 是部署单元，`dist/server` 目录是对应 Linux 运行包；源码位于 `src/Server`。

## 源码与构建

公开仓库：[MengLeiFudge/MLJ_PlateUp_Mods](https://github.com/MengLeiFudge/MLJ_PlateUp_Mods)。可从仓库的 **Code → Download ZIP** 下载，或使用[完整源码 ZIP 链接](https://github.com/MengLeiFudge/MLJ_PlateUp_Mods/archive/refs/heads/main.zip)。解压完整仓库后，在本模组目录执行下方构建命令（从仓库根目录先执行 `cd .\mods\PlateUpRelay`）；服务端项目会引用 `src/Shared`，不要只下载 `src/Server`。

普通玩家使用管理员提供的中转服务，无需编译源码或单独部署服务器。管理员只需发布服务端；服务端构建不依赖游戏程序集。所需 .NET SDK 由 `src/Server/PlateUpRelay.Server.csproj` 的 `TargetFramework` 决定。

- `src/Mod/RelayMod.cs`：创意工坊入口、Harmony 传输接入与配置文件读写。
- `src/Mod/RelaySettingsMenu.cs`：主菜单及暂停菜单中的中转设置、原生地址输入。
- `src/Mod/RelayLatencyHud.cs`：版本文字上方的常驻延迟显示。
- `src/Mod/RelayText.cs`：中英文面板消息，显示时按游戏语言选择文案。
- `src/Mod/RelaySteamTransport.cs`：Steam 房间生命周期、初始化凭据及对局消息收发。
- `src/Shared/Protocol.cs`：可靠 UDP 参数和双方共享协议。
- `src/Server/Program.cs`：房间隔离、连接管理和消息转发。

使用 Windows .NET SDK：

```powershell
dotnet build .\src\Mod\PlateUpRelay.csproj -c Release
dotnet publish .\src\Server\PlateUpRelay.Server.csproj -c Release -r linux-x64 --self-contained true -o .\dist\server
```

模组项目的 `GameDir` 和 `HarmonyPath` 默认指向本机安装位置，在其他电脑编译时可通过 MSBuild 参数覆盖。游戏程序集只作本地编译引用，不包含在分发包中。
