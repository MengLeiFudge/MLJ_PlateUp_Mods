# 游戏中转模组

## 概览

保留 Steam 房间和邀请，以自建 UDP 服务转发房主与玩家之间的对局数据；本文件中的路径均相对此模组目录。

## 结构

```text
PlateUpRelay/
├── src/Mod/                  # 客户端及其专属 AGENTS
├── src/Server/               # 独立中转服务
├── src/Shared/Protocol.cs    # 两个项目链接编译
├── assets/                   # 封面与绘制脚本
├── docs/                     # 中英文工坊介绍
├── scripts/                  # 群友单文件 BAT
├── deploy/                   # systemd 单元
├── licenses/                 # 第三方许可原文
├── workshop/                # 上传器的 Mod Directory
└── dist/                    # ZIP 与 Linux 发布包
```

## 去哪里找

| 任务 | 位置 | 说明 |
| --- | --- | --- |
| 修改游戏接入、设置或传输 | `src/Mod/AGENTS.md` | 生命周期、握手与 UI |
| 修改消息格式和可靠发送参数 | `src/Shared/Protocol.cs` | `Magic`、`Message`、`CreateManager`、`Send` |
| 修改连接准入与房间隔离 | `src/Server/Program.cs` | `RelayServer.Accept`、`Room`、`Session` |
| 修改就绪通知和路由 | `src/Server/Program.cs` | `Connected`、`Disconnected`、`Receive` |
| 修改构建引用 | `src/Mod/PlateUpRelay.csproj`、`src/Server/PlateUpRelay.Server.csproj` | 共享源码与游戏程序集引用 |
| 修改安装流程 | `scripts/PlateUpRelay一键安装.bat` | 订阅状态、下载、备份与配置写入 |
| 修改部署约束 | `deploy/plateup-relay.service` | 启动参数、临时用户与资源限制 |
| 安装、日志、上传与构建 | `README.md` | 模组操作入口 |
| 修改工坊说明 | `docs/Steam工坊说明.txt`、`docs/Steam工坊说明.en.txt` | 保持双语步骤对应 |
| 修改封面 | `assets/render_cover.py` | 同目录输出 `工坊封面.png` |

## 约定

- `Protocol` 通过两个项目的 `Compile Include` / `Link` 共同编译；协议改动同时影响客户端与服务端。
- `Protocol.Message` 的地址字段：客户端发送时为目标；服务端转发时替换为连接绑定的发送者。
- 房主通过 Steam 通道分发凭据；服务端以 Lobby ID 和凭据哈希索引房间，将 `Session` 绑定到 `NetPeer.Tag`。
- 仅允许房主与玩家互通；拒绝自发自收和玩家直发其他玩家，目标不可用时通知 `Left`。
- `Connected` 先通知就绪成员，再开放游戏同步；服务端自身不模拟游戏逻辑。
- 可靠发送经 `Protocol.Send`；队列超限断开连接，不静默丢弃可靠消息。资源边界与人数规则分离。
- 往返测量沿用已认证房间；服务端仅接受玩家 `Ping`、房主 `Pong`，载荷为固定长度序号。
- 群友 BAT 保留工坊订阅，只将中转模组复制到本地；备份在游戏目录的 `PlateUpRelay.Backups`，位于 Mods 外。
- 配置替换为安装的最后提交步骤；PowerShell 调用 `File.Replace` 使用 `Language.NullString.Value` 表达真实空引用。
- 客户端与协议使用 `PlateUpRelay` 块命名空间；服务端入口位于全局命名空间，启用 nullable 和隐式 using。
- 服务端由 LiteNetLib 回调处理状态，`Run` 轮询事件；Ctrl+C / SIGTERM 通过 `Volatile` 停止循环，`Dispose` 释放网络。
- 上传器读取 `workshop/content`，工坊身份取自 `workshop/plateup_mod_metadata.json`；ZIP 保持 `content` 与元数据位于顶层。
- Windows 构建命令在本模组目录执行，服务端发布到 `dist/server`；线上安装目录仍由服务单元定义为 `/opt/plateup-relay`。
- 封面脚本使用全局画布和缩放 helper，再以 LANCZOS 缩小；依赖 Pillow 与 `FONT` 指定的本机 Noto CJK 字体。
- 执行或导入封面脚本会覆盖 `assets/工坊封面.png`；静态检查不要导入该脚本。
- Steam 初始化通道与 UDP 连通状态分别判断；房主仍须在线，通道不提供额外端到端加密。

## 禁止事项

- 不同时从本地 Mods 和工坊加载同一中转模组副本；安装边界见 README。
- 不移除 LiteNetLib 版权与许可声明；保留 `licenses/LiteNetLib-LICENSE.txt` 及上传内容中的对应文件。
- 不将 `src/Shared` 当作其他内容模组的默认依赖；其中的协议与服务器地址解析属于本中转模组。
