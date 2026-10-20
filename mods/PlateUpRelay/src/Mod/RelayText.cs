using KitchenData;

namespace PlateUpRelay
{
    /// <summary>一条中英文面板消息；显示时读取游戏语言，使已保存的连接状态也能切换语言。</summary>
    internal readonly struct RelayText
    {
        private readonly string chinese;
        private readonly string english;

        /// <summary>保存两种语言的消息；系统异常详情可在两种语言中保留原文。</summary>
        /// <param name="chinese">中文消息。</param>
        /// <param name="english">英文消息。</param>
        public RelayText(string chinese, string english)
        {
            this.chinese = chinese;
            this.english = english;
        }

        /// <summary>中文游戏语言使用中文，其他语言使用英文；默认值表示没有消息。</summary>
        public string Value => Localisation.CurrentLocale == Locale.ChineseSimplified ||
            Localisation.CurrentLocale == Locale.ChineseTraditional ? chinese : english;

        /// <summary>面板标题。</summary>
        public static readonly RelayText Title = new RelayText("游戏中转", "Game relay");
        /// <summary>地址输入标签。</summary>
        public static readonly RelayText AddressLabel = new RelayText(
            "中转服务器 IPv4（默认 UDP 27841，可填 IP:端口）", "Relay IPv4 address (default UDP 27841; IP:port accepted)");
        /// <summary>服务器连接与往返延迟标签。</summary>
        public static readonly RelayText ServerStatusLabel = new RelayText("服务器：", "Server: ");
        /// <summary>房间加入与到房主往返延迟标签。</summary>
        public static readonly RelayText RoomStatusLabel = new RelayText("房间：", "Lobby: ");
        /// <summary>多人房间中的设置限制。</summary>
        public static readonly RelayText LeaveLobbyToChange = new RelayText(
            "请先离开当前多人房间，再修改地址。", "Leave the multiplayer lobby before changing the address.");
        /// <summary>保存后的联机操作提示。</summary>
        public static readonly RelayText InviteHint = new RelayText(
            "保存后正常邀请朋友；所有玩家填写同一个服务器。", "Save, then invite friends as usual. Everyone must use the same relay.");
        /// <summary>保存并启用按钮。</summary>
        public static readonly RelayText SaveAndEnable = new RelayText("保存并启用", "Save and enable");
        /// <summary>关闭中转按钮。</summary>
        public static readonly RelayText DisableRelay = new RelayText(
            "关闭中转，使用原联机", "Disable relay and use the original connection");
        /// <summary>返回上级设置菜单。</summary>
        public static readonly RelayText Back = new RelayText("返回", "Back");
        /// <summary>原生文本输入尚未就绪或正被使用。</summary>
        public static readonly RelayText InputUnavailable = new RelayText(
            "暂时无法打开输入框，请关闭其他输入框后重试。",
            "Text entry is unavailable. Close any other text entry dialog and try again.");

        /// <summary>显示可编辑地址；未填写时提示输入。</summary>
        /// <param name="address">当前编辑中的地址草稿。</param>
        /// <returns>原生地址输入按钮的中英文标题。</returns>
        public static RelayText EditAddress(string address) => new RelayText(
            "中转地址：" + (string.IsNullOrWhiteSpace(address) ? "点击输入" : address),
            "Relay address: " + (string.IsNullOrWhiteSpace(address) ? "enter address" : address));
        /// <summary>空地址校验提示。</summary>
        public static readonly RelayText EnterAddress = new RelayText("请输入服务器 IP。", "Enter the server IP address.");
        /// <summary>无效地址校验提示。</summary>
        public static readonly RelayText InvalidAddress = new RelayText(
            "请输入有效的 IPv4 地址，可附加 :端口。", "Enter a valid IPv4 address, optionally followed by :port.");
        /// <summary>修改设置前的离房提示。</summary>
        public static readonly RelayText LeaveLobby = new RelayText("请先离开多人房间。", "Leave the multiplayer lobby first.");
        /// <summary>设置保存成功提示。</summary>
        public static readonly RelayText Saved = new RelayText(
            "已保存并启用。请按原来的 Steam 方式邀请或加入房间。",
            "Saved and enabled. Invite friends or join a lobby through Steam as usual.");
        /// <summary>中转未启用状态。</summary>
        public static readonly RelayText Disabled = new RelayText("未启用中转 · 往返：—", "Relay disabled · RTT: —");
        /// <summary>Steam 房间尚未就绪。</summary>
        public static readonly RelayText WaitingLobby = new RelayText(
            "未连接（已启用，等待 Steam 房间） · 往返：—", "Not connected (enabled; waiting for Steam lobby) · RTT: —");
        /// <summary>等待房主提供房间凭据。</summary>
        public static readonly RelayText WaitingCredentials = new RelayText(
            "未连接，等待房主确认身份 · 往返：—", "Not connected; waiting for host credentials · RTT: —");
        /// <summary>服务器已连通但还没有延迟样本。</summary>
        public static readonly RelayText ServerMeasuring = new RelayText(
            "已连接 · 往返：测量中", "Connected · RTT: measuring");
        /// <summary>没有活动的 Steam 房间。</summary>
        public static readonly RelayText RoomNone = new RelayText(
            "未加入 · 到房主往返：—", "Not joined · Host RTT: —");
        /// <summary>Steam 异步建房或入房尚未完成。</summary>
        public static readonly RelayText RoomJoining = new RelayText(
            "正在创建或加入 · 到房主往返：—", "Creating or joining · Host RTT: —");
        /// <summary>正在离开 Steam 房间。</summary>
        public static readonly RelayText RoomLeaving = new RelayText(
            "正在离开 · 到房主往返：—", "Leaving · Host RTT: —");
        /// <summary>本机是房主，不显示虚构的远端往返样本。</summary>
        public static readonly RelayText RoomHosting = new RelayText(
            "已创建 · 本机房主", "Created · You are the host");
        /// <summary>原联机路径未提供可用的房间往返样本。</summary>
        public static readonly RelayText RoomOriginal = new RelayText(
            "已加入（原联机） · 到房主往返：—", "Joined (original connection) · Host RTT: —");
        /// <summary>Steam 已入房，尚未具备中转到房主的条件。</summary>
        public static readonly RelayText RoomWaitingHost = new RelayText(
            "已加入，等待房主中转就绪 · 到房主往返：—", "Joined; waiting for host relay · Host RTT: —");
        /// <summary>本机 UDP socket 无法启动。</summary>
        public static readonly RelayText SocketUnavailable = new RelayText(
            "无法打开本机 UDP socket", "Could not open a local UDP socket");

        /// <summary>显示与服务器之间的往返延迟。</summary>
        /// <param name="ping">服务器往返延迟，单位为毫秒。</param>
        /// <returns>带往返延迟的已连接状态。</returns>
        public static RelayText Connected(int ping) => new RelayText(
            "已连接 · 往返：" + ping + " ms", "Connected · RTT: " + ping + " ms");

        /// <summary>显示经中转到房主的实测往返；缺失或过期样本显示为待测。</summary>
        /// <param name="ping">有效样本的毫秒值；空值表示待测或超时。</param>
        /// <returns>房间加入状态及到房主往返。</returns>
        public static RelayText RoomJoined(int? ping) => new RelayText(
            "已加入 · 到房主往返：" + (ping.HasValue ? ping.Value + " ms" : "—（待测）"),
            "Joined · Host RTT: " + (ping.HasValue ? ping.Value + " ms" : "— (pending)"));

        /// <summary>保留中转异常详情。</summary>
        /// <param name="message">异常提供的原始消息。</param>
        /// <returns>带异常详情的中转状态。</returns>
        public static RelayText Failure(string message) => new RelayText("中转异常：" + message, "Relay error: " + message);

        /// <summary>显示断线原因及重试提示。</summary>
        /// <param name="reason">LiteNetLib 的断线原因名称。</param>
        /// <returns>带断线原因的重试状态。</returns>
        public static RelayText Disconnected(string reason) => new RelayText(
            reason == "ConnectionRejected" ? "连接被拒绝；请核对服务端版本与房间配置 · 往返：—" : "未连接：" + reason + "，稍后重试 · 往返：—",
            reason == "ConnectionRejected" ? "Connection rejected; check server version and lobby settings · RTT: —" : "Disconnected: " + reason + "; retrying · RTT: —");

        /// <summary>保留 UDP 网络错误代码。</summary>
        /// <param name="error">socket 错误名称。</param>
        /// <returns>带错误名称的网络状态。</returns>
        public static RelayText NetworkError(string error) => new RelayText("UDP 网络错误：" + error, "UDP network error: " + error);

        /// <summary>显示正在连接的服务器地址。</summary>
        /// <param name="address">用户保存的服务器地址。</param>
        /// <returns>带目标地址的连接状态。</returns>
        public static RelayText Connecting(string address) => new RelayText(
            "正在连接 " + address + " · 往返：—", "Connecting to " + address + " · RTT: —");
    }
}
