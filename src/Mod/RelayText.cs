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
        public static readonly RelayText Title = new RelayText("PlateUp 游戏中转 · F8", "PlateUp Relay · F8");
        /// <summary>服务器程序要求。</summary>
        public static readonly RelayText ServerRequired = new RelayText(
            "服务器须运行配套程序 PlateUpRelay.Server。", "The server must run PlateUpRelay.Server.");
        /// <summary>仅放行端口不能提供中转。</summary>
        public static readonly RelayText PortNotEnough = new RelayText(
            "仅开放 UDP 端口无法中转游戏数据。", "Opening a UDP port alone does not provide a relay.");
        /// <summary>地址输入标签。</summary>
        public static readonly RelayText AddressLabel = new RelayText(
            "中转服务器 IPv4（默认 UDP 27841，可填 IP:端口）", "Relay IPv4 address (default UDP 27841; IP:port accepted)");
        /// <summary>连接状态标签。</summary>
        public static readonly RelayText StatusLabel = new RelayText("状态：", "Status: ");
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
        /// <summary>关闭窗口按钮。</summary>
        public static readonly RelayText CloseWindow = new RelayText("关闭窗口", "Close window");
        /// <summary>空地址校验提示。</summary>
        public static readonly RelayText EnterAddress = new RelayText("请输入服务器 IP。", "Enter the server IP address.");
        /// <summary>无效地址校验提示。</summary>
        public static readonly RelayText InvalidAddress = new RelayText(
            "请输入有效的 IPv4 地址，可附加 :端口。", "Enter a valid IPv4 address, optionally followed by :port.");
        /// <summary>修改设置前的离房提示。</summary>
        public static readonly RelayText LeaveLobby = new RelayText("请先离开多人房间。", "Leave the multiplayer lobby first.");
        /// <summary>设置保存成功提示。</summary>
        public static readonly RelayText Saved = new RelayText("已保存", "Saved");
        /// <summary>中转未启用状态。</summary>
        public static readonly RelayText Disabled = new RelayText("未启用中转", "Relay disabled");
        /// <summary>Steam 房间尚未就绪。</summary>
        public static readonly RelayText WaitingLobby = new RelayText("等待 Steam 房间", "Waiting for the Steam lobby");
        /// <summary>等待房主提供房间凭据。</summary>
        public static readonly RelayText WaitingCredentials = new RelayText(
            "等待房主模组确认身份（房主也需开启中转）", "Waiting for the host to confirm your identity (the host must enable the relay)");
        /// <summary>已连接服务器，等待房主中转就绪。</summary>
        public static readonly RelayText WaitingHost = new RelayText(
            "服务器已连接，等待房主连接同一服务器", "Server connected; waiting for the host to connect to the same relay");
        /// <summary>本机 UDP socket 无法启动。</summary>
        public static readonly RelayText SocketUnavailable = new RelayText(
            "无法打开本机 UDP socket", "Could not open a local UDP socket");

        /// <summary>显示与服务器之间的往返延迟。</summary>
        /// <param name="ping">服务器往返延迟，单位为毫秒。</param>
        /// <returns>带往返延迟的已连接状态。</returns>
        public static RelayText Connected(int ping) => new RelayText(
            "服务器已连接 · 往返 " + ping + " ms", "Server connected · Round trip: " + ping + " ms");

        /// <summary>保留中转异常详情。</summary>
        /// <param name="message">异常提供的原始消息。</param>
        /// <returns>带异常详情的中转状态。</returns>
        public static RelayText Failure(string message) => new RelayText("中转异常：" + message, "Relay error: " + message);

        /// <summary>显示断线原因及重试提示。</summary>
        /// <param name="reason">LiteNetLib 的断线原因名称。</param>
        /// <returns>带断线原因的重试状态。</returns>
        public static RelayText Disconnected(string reason) => new RelayText(
            "中转未连接：" + reason + "，稍后重试", "Relay disconnected: " + reason + "; retrying shortly");

        /// <summary>保留 UDP 网络错误代码。</summary>
        /// <param name="error">socket 错误名称。</param>
        /// <returns>带错误名称的网络状态。</returns>
        public static RelayText NetworkError(string error) => new RelayText("UDP 网络错误：" + error, "UDP network error: " + error);

        /// <summary>显示正在连接的服务器地址。</summary>
        /// <param name="address">用户保存的服务器地址。</param>
        /// <returns>带目标地址的连接状态。</returns>
        public static RelayText Connecting(string address) => new RelayText("正在连接 " + address, "Connecting to " + address);
    }
}
