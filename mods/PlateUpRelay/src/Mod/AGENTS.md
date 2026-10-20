# 模组接入

## 概览

承接游戏加载、原生设置菜单、版本文字上方的延迟 HUD 与 Steam 传输包装；网络事件在游戏主线程轮询。

## 去哪里找

| 任务 | 位置 | 说明 |
| --- | --- | --- |
| 游戏加载与 Harmony 安装 | `RelayMod.cs` → `RelayMod.PostActivate` | 静态 `loaded` 防止重复初始化 |
| 替换传输工厂 | `RelayMod.cs` → `TransportFactoryPatch` | 包装 `SteamNetworkService.GetNewTransport` |
| 屏蔽原生对局包 | `RelayMod.cs` → `NativeReceivePatch` | 拦截 `SteamLobbyTransport.HandleMessage` |
| 地址持久化和校验 | `RelayMod.cs` → `RelaySettings` | `ConfigurationPath`、IPv4 解析、`Revision` |
| 设置菜单与地址输入 | `RelaySettingsMenu.cs` | `OptionsMenu` 子菜单、`TextInputView`、`Apply` |
| 延迟文字与生命周期 | `RelayLatencyHud.cs` | 随 `DisplayVersion` 创建，复用字体与局部布局 |
| 等待握手和房主就绪 | `RelaySteamTransport.cs` → `Update`、`SendStatus` | `ready` 与 `LobbyMembers` 的协调 |
| Steam 凭据交换 | `RelaySteamTransport.cs` → `PollBootstrap`、`RequestKey` | 房间成员及房主身份检查 |
| UDP 连接与收包 | `RelaySteamTransport.cs` → `EnsureConnection`、`Receive` | 连接凭据、断线重连、消息筛选 |
| 离房与配置变更清理 | `RelaySteamTransport.cs` → `StopRelay` | 停止网络并清空凭据、成员和重试时刻 |
| 修改菜单语言与连接状态文案 | `RelayText.cs` | `RelayText.Value` 按 `Localisation.CurrentLocale` 选择中文或英文 |
| 服务器/房间延迟与 HUD 数据 | `RelaySteamTransport.cs` | `GetLatency`、`RoomStatus`、`PollRoomPing`、`Receive` |

## 约定

- 即使关闭中转，工厂仍返回 `RelaySteamTransport`；禁用分支将发送交回 `SteamLobbyTransport`。
- `PostActivate` 仅加载设置和补丁；设置菜单随 `OptionsMenu` 创建，HUD 随 `DisplayVersion` 创建并释放。
- 菜单返回与中转关闭是不同操作；文本输入交由游戏处理粘贴、取消和输入锁，不接管光标。
- HUD 复用版本文字的字体、字号、材质和缩放，按 TMP 实际行高上移；只销毁自建文本，不销毁游戏共享资源。
- `RelaySettings.TryParse` 仅接受 IPv4 和可选端口；空地址表示禁用。`Save` 原子替换配置文件后增加 `Revision`。
- `ConfigurationPath` 是游戏持久化目录下的 `PlateUpRelay/server.txt`，与群友脚本一致；仅在文件不存在时兼容读取旧 PlayerPrefs。
- `Update` 先调用基类；房间、房主或配置修订变化后重建中转状态，房主用随机数生成临时凭据。
- `PollBootstrap` 检查消息长度、Magic、Lobby ID 和成员身份；凭据回复仅接受当前房主且本机尚无凭据的情况。
- `SendStatus` 在中转未连通或客户端尚未收到房主就绪通知时暂停发送；`IsTargetConnected` 沿用基类语义。
- `Update` 按 `ready` 筛选 `LobbyMembers`；收取游戏数据还须核对发送者仍在 Steam 房间，客户端只收房主数据。
- `Dispose`、`RequestDisconnect`、`RequestSuspend` 均先清理中转再调用基类；`StopRelay` 清零凭据数组后释放引用。
- 传输异常会清理状态并暂缓重试；设置写入失败不得改变运行时地址或误报启用成功。
- Unity 日志统一使用 `[PlateUpRelay]` 前缀；连接阶段分别记录 Steam 房间就绪、凭据收到、UDP 连通和房主就绪。
- `RelayText` 保存中英文消息，显示时读取游戏语言；系统异常详情保留原文。不要把普通连接状态预先转换成某种语言的字符串缓存。
- 服务器往返读 `NetPeer.RoundTripTime`，不能用半程估算 `Ping`；房间往返使用单个待响应序号，超时、离房或断线后不沿用旧值。

## 禁止事项

- 不在 `PostActivate` 创建依赖游戏 UI 的对象；该阶段早于菜单与版本文字初始化。
- 启用中转时不得让原生 P2P 对局包再次进入接收路径；保留 `NativeReceivePatch` 的屏蔽条件。
- 有远端玩家时不得切换地址或关闭中转；保留菜单禁用与 `RelaySettings.Save` 的二次检查。
- 不把异步入房尚未完成直接判为断开；通过 `SendStatus` 暂停发送，保留游戏原有活性超时处理。
