# 项目知识库

## 概览

PlateUp Steam 联机中转模组：保留 Steam 房间与邀请，将对局消息经自建 UDP 服务在房主与玩家之间转发。
核心为 C#、Harmony、Unity ECS 和 LiteNetLib；Python/Pillow 脚本绘制工坊封面。

## 结构

```text
.
├── src/
│   ├── Mod/                  # 游戏入口、设置面板与 Steam 传输接入
│   ├── Server/               # 中转服务源码
│   ├── Shared/Protocol.cs    # 两个项目直接链接编译的共享源码
│   ├── NuGet.Config
│   └── packages/             # 本地 NuGet 包源
├── server/                   # Linux 发布包，非服务端源码
├── workshop/
│   ├── plateup_mod_metadata.json
│   └── content/              # 上传器实际读取的分发内容
├── assets/render_cover.py
├── plateup-relay.service
└── 使用说明.md
```

## 去哪里找

| 任务 | 位置 | 说明 |
| --- | --- | --- |
| 修改游戏接入、设置或传输 | `src/Mod/AGENTS.md` | 模组生命周期及握手约束 |
| 修改消息格式、可靠发送参数 | `src/Shared/Protocol.cs` | `Magic`、消息种类、`CreateManager`、`Message`、`Send` |
| 修改连接准入与房间隔离 | `src/Server/Program.cs` | `RelayServer.Accept`、`Room`、`Session` |
| 修改就绪通知与消息路由 | `src/Server/Program.cs` | `Connected`、`Disconnected`、`Receive` |
| 修改编译引用与依赖 | `src/Mod/PlateUpRelay.csproj`、`src/Server/PlateUpRelay.Server.csproj`、`src/NuGet.Config` | 游戏程序集路径与本地包源 |
| 修改部署约束 | `plateup-relay.service` | 工作目录、启动参数、临时用户和资源限制 |
| 安装、日志定位、工坊更新 | `使用说明.md` | 安装流程及官方上传器操作 |
| 修改封面 | `assets/render_cover.py` | 输出根目录 `工坊封面.png` |

## 代码地图

| 符号 | 类型 | 位置 | 引用 | 作用 |
| --- | --- | --- | --- | --- |
| `Protocol` | 内部静态类 | `src/Shared/Protocol.cs` | `RelaySteamTransport`、`RelayServer`、`RelaySettings` | 共享消息编码与发送边界 |
| `RelaySettings` | 内部静态类 | `src/Mod/RelayMod.cs` | `RelayPanel`、`RelaySteamTransport`、Harmony 补丁 | 地址、配置修订与连接状态 |
| `RelaySteamTransport` | 传输类 | `src/Mod/RelaySteamTransport.cs` | `TransportFactoryPatch`、`NativeReceivePatch`、`RelayPanel` | Steam 生命周期与 UDP 中转桥接 |
| `RelayPanel` | Unity 组件 | `src/Mod/RelayMod.cs` | `RelayPanelSystem` | 游戏内设置窗口 |
| `RelayPanelSystem` | ECS 系统 | `src/Mod/RelayPanelSystem.cs` | 游戏通过 `IModSystem` 接入 | 持有并释放面板 |
| `RelayServer` | 服务入口类 | `src/Server/Program.cs` | `Main` | 连接管理、轮询与转发 |

## 约定

- `src/Shared/Protocol.cs` 通过两个项目的 `Compile Include` / `Link` 共同编译；协议改动同时影响客户端与服务端。
- `Protocol.Message` 的地址字段：客户端发送时为目标；服务端转发时用连接绑定的发送者替换。
- 连接凭据来自房主的 Steam 通道；服务端以 Lobby ID 和凭据哈希索引房间，将 `Session` 绑定到 `NetPeer.Tag`。
- 路由只允许房主与玩家互通；`Receive` 拒绝自发自收和玩家直发其他玩家，目标不可用时通知 `Left`。
- `Connected` 先通知就绪成员；客户端据此开放游戏同步。服务端自身不模拟游戏逻辑。
- 可靠发送统一经 `Protocol.Send`；队列超限断开连接，不静默丢弃可靠消息。消息资源边界与玩家人数规则分离。
- `src/NuGet.Config` 清除继承的包源，再声明 `packages` 与 nuget.org；本地路径相对该配置文件。

## 禁止事项

- 不随分发包附带游戏本体程序集；模组项目将这些引用标记为 `Private=false`。
- 不同时从本地 Mods 和工坊加载同一模组副本；安装边界见 `使用说明.md`。
- 分发 LiteNetLib 时不得移除其版权和许可声明；保留 `LiteNetLib-LICENSE.txt`。

## 独特风格

- 手写说明、类型摘要和行为注释主要为简体中文；游戏面板文案为中文，诊断日志主要为英文。
- 模组与共享协议使用 `PlateUpRelay` 块命名空间；服务端入口在全局命名空间，启用 nullable 和隐式 using。
- 服务端通过 LiteNetLib 事件回调处理状态；`Run` 轮询事件，Ctrl+C / SIGTERM 通过 `Volatile` 停止循环，`Dispose` 停止网络。
- 封面脚本使用全局画布与缩放绘图 helper，再以 LANCZOS 缩小；字体路径固定在脚本的 `FONT`。

## 命令

在项目根目录使用 Windows .NET SDK；命令来源为 `使用说明.md` 的“源码与构建”，对应项目文件均在 `src` 下：

```powershell
dotnet build .\src\Mod\PlateUpRelay.csproj -c Release
dotnet publish .\src\Server\PlateUpRelay.Server.csproj -c Release -r linux-x64 --self-contained true -o .\server
```

部署启动由 `plateup-relay.service` 的 `ExecStart` 定义：

```text
/opt/plateup-relay/PlateUpRelay.Server 27841
```

服务管理命令见 `使用说明.md` 的“服务器维护”。未发现项目级格式化、lint、静态检查或 CI 命令配置。

## 注意

- `GameDir` 指向游戏的 `PlateUp_Data/Managed`，`HarmonyPath` 指向 Harmony DLL；默认值为本机 Windows 路径，可通过 MSBuild 属性覆盖。
- `server/`、`src/*/bin/`、`src/*/obj/` 与 `workshop/content/` 中的 DLL 属于产物；源码修改不会自动替换已安装或已上传副本。
- 上传器选择 `workshop` 为 Mod Directory，并读取其 `content`；工坊条目标识来自 `workshop/plateup_mod_metadata.json`。
- `assets/render_cover.py` 依赖 Pillow 和本机 Noto CJK 字体；执行或导入都会绘图并覆盖根目录封面。
- Steam 初始化通道与中转服务均不可被“UDP 已连接”这一状态替代；房主仍须在线，该通道不提供额外端到端加密。
- 使用说明中的工坊加载限制与账号发布资格涉及外部状态，操作前重新核实，不将其视为源码保证。
