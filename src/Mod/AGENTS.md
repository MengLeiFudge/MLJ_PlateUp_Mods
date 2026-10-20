# 模组接入

## 概览

承接游戏加载、F8 设置面板与 Steam 传输包装；网络事件在游戏主线程轮询。

## 去哪里找

| 任务 | 位置 | 说明 |
| --- | --- | --- |
| 游戏加载与 Harmony 安装 | `RelayMod.cs` → `RelayMod.PostActivate` | 静态 `loaded` 防止重复初始化 |
| 替换传输工厂 | `RelayMod.cs` → `TransportFactoryPatch` | 包装 `SteamNetworkService.GetNewTransport` |
| 屏蔽原生对局包 | `RelayMod.cs` → `NativeReceivePatch` | 拦截 `SteamLobbyTransport.HandleMessage` |
| 地址持久化和校验 | `RelayMod.cs` → `RelaySettings` | PlayerPrefs、IPv4 解析、`Revision` |
| 窗口与快捷键 | `RelayMod.cs` → `RelayPanel` | `PollShortcut`、`OnGUI`、`Apply` |
| 面板生命周期 | `RelayPanelSystem.cs` | `OnUpdate` 创建，`ReleasePanel` 释放 |
| 等待握手和房主就绪 | `RelaySteamTransport.cs` → `Update`、`SendStatus` | `ready` 与 `LobbyMembers` 的协调 |
| Steam 凭据交换 | `RelaySteamTransport.cs` → `PollBootstrap`、`RequestKey` | 房间成员及房主身份检查 |
| UDP 连接与收包 | `RelaySteamTransport.cs` → `EnsureConnection`、`Receive` | 连接凭据、断线重连、消息筛选 |
| 离房与配置变更清理 | `RelaySteamTransport.cs` → `StopRelay` | 停止网络并清空凭据、成员和重试时刻 |

## 约定

- 即使关闭中转，工厂仍返回 `RelaySteamTransport`；禁用分支将发送交回 `SteamLobbyTransport`。
- `PostActivate` 仅加载设置和补丁；游戏世界中的 `RelayPanelSystem` 拥有面板，输入接口可用后才轮询 F8。
- 面板关闭与中转关闭是不同操作；窗口打开时保存光标状态，关闭或销毁时恢复，销毁时释放动态字体。
- `OnGUI` 恢复共享 `GUI.skin.font`；保留 `ExitGUIException` 的重新抛出路径。
- `RelaySettings.TryParse` 仅接受 IPv4 和可选端口；空地址表示禁用。`Save` 持久化后增加 `Revision`。
- `Update` 先调用基类；房间、房主或配置修订变化后重建中转状态，房主用随机数生成临时凭据。
- `PollBootstrap` 检查消息长度、Magic、Lobby ID 和成员身份；凭据回复仅接受当前房主且本机尚无凭据的情况。
- `SendStatus` 在中转未连通或客户端尚未收到房主就绪通知时暂停发送；`IsTargetConnected` 沿用基类语义。
- `Update` 按 `ready` 筛选 `LobbyMembers`；收取游戏数据还须核对发送者仍在 Steam 房间，客户端只收房主数据。
- `Dispose`、`RequestDisconnect`、`RequestSuspend` 均先清理中转再调用基类；`StopRelay` 清零凭据数组后释放引用。
- 传输异常会清理状态并暂缓重试；ECS 初始化或输入异常会停用该系统，防止每帧重复报错。
- Unity 日志统一使用 `[PlateUpRelay]` 前缀；连接阶段分别记录 Steam 房间就绪、凭据收到、UDP 连通和房主就绪。

## 禁止事项

- 不在 `PostActivate` 创建依赖游戏世界或输入系统的面板；该阶段早于这些系统初始化。
- 启用中转时不得让原生 P2P 对局包再次进入接收路径；保留 `NativeReceivePatch` 的屏蔽条件。
- 有远端玩家时不得切换地址或关闭中转；保留 UI 禁用与 `Apply` 的二次检查。
- 不把异步入房尚未完成直接判为断开；通过 `SendStatus` 暂停发送，保留游戏原有活性超时处理。
