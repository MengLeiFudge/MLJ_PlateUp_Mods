using System;
using System.Net;
using HarmonyLib;
using Kitchen;
using Kitchen.NetworkSupport;
using Kitchen.Transports;
using KitchenMods;
using UnityEngine;
using Font = UnityEngine.Font;

namespace PlateUpRelay
{
    /// <summary>原生创意工坊入口；仅替换 Steam 对局传输，保留游戏房间管理。</summary>
    public sealed class RelayMod : IModInitializer
    {
        private static bool loaded;

        public void PostActivate(Mod mod)
        {
            if (loaded)
                return;
            RelaySettings.Load();
            new Harmony("local.plateup.server-relay").PatchAll(typeof(RelayMod).Assembly);
            // 此阶段早于游戏世界及输入系统初始化；面板交由 RelayPanelSystem 创建。
            loaded = true;
            Debug.Log("[PlateUpRelay] v0.1.2 loaded; waiting for the game UI system.");
        }

        public void PreInject() { }
        public void PostInject() { }
    }

    /// <summary>启用与关闭都经过同一包装类；空地址时完整保留原 Steam 通道。</summary>
    [HarmonyPatch(typeof(SteamNetworkService), nameof(SteamNetworkService.GetNewTransport))]
    internal static class TransportFactoryPatch
    {
        private static bool Prefix(JoinCode join_code, ref INetworkTransport __result)
        {
            __result = new RelaySteamTransport(join_code);
            return false;
        }
    }

    /// <summary>中转启用时阻止原生 P2P 对局包混入，避免同一消息从两条路径到达。</summary>
    [HarmonyPatch(typeof(SteamLobbyTransport), "HandleMessage")]
    internal static class NativeReceivePatch
    {
        private static bool Prefix(SteamLobbyTransport __instance)
        {
            return !(__instance is RelaySteamTransport) || !RelaySettings.Enabled;
        }
    }

    /// <summary>本机只保存服务器地址；玩家身份及房间凭据自动从 Steam 会话获取。</summary>
    internal static class RelaySettings
    {
        private const string PreferenceKey = "PlateUpRelay.Server";
        public static string Address { get; private set; } = "";
        public static IPEndPoint Endpoint { get; private set; }
        public static int Revision { get; private set; }
        public static bool Enabled => Endpoint != null;
        public static string Status = "未启用中转";

        public static void Load()
        {
            string saved = PlayerPrefs.GetString(PreferenceKey, "");
            if (TryParse(saved, out var endpoint))
            {
                Address = saved;
                Endpoint = endpoint;
            }
        }

        public static bool TryParse(string text, out IPEndPoint endpoint)
        {
            endpoint = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            string[] parts = text.Trim().Split(':');
            int port = Protocol.Port;
            if (parts.Length > 2 || !IPAddress.TryParse(parts[0], out var ip) ||
                ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
                (parts.Length == 2 && (!int.TryParse(parts[1], out port) || port < 1 || port > 65535)))
                return false;
            endpoint = new IPEndPoint(ip, port);
            return true;
        }

        public static void Save(string text)
        {
            if (!TryParse(text, out var endpoint))
                throw new ArgumentException("请输入有效的 IPv4 地址，可附加 :端口。");
            string normalized = text.Trim();
            PlayerPrefs.SetString(PreferenceKey, normalized);
            PlayerPrefs.Save();
            Address = normalized;
            Endpoint = endpoint;
            Revision++;
        }
    }

    /// <summary>F8 打开游戏内地址设置；只在没有远端玩家时切换，避免打断进行中的对局。</summary>
    public sealed class RelayPanel : MonoBehaviour
    {
        private bool visible;
        private string input = "";
        private string error = "";
        private Rect window = new Rect(40, 80, 540, 290);
        private Font font;
        private bool loggedDrawing;
        private bool oldCursorVisible;
        private CursorLockMode oldCursorLock;

        /// <summary>由游戏的 ECS 更新循环调用，不依赖预加载阶段创建的 Behaviour 更新。</summary>
        internal void PollShortcut()
        {
            if (Input.GetKeyDown(KeyCode.F8))
            {
                if (visible)
                    Close();
                else
                {
                    input = RelaySettings.Address;
                    error = "";
                    oldCursorVisible = Cursor.visible;
                    oldCursorLock = Cursor.lockState;
                    visible = true;
                    Debug.Log("[PlateUpRelay] F8 received; opening settings.");
                }
            }
        }

        private void LateUpdate()
        {
            if (visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Close()
        {
            visible = false;
            Cursor.lockState = oldCursorLock;
            Cursor.visible = oldCursorVisible;
        }

        private void OnGUI()
        {
            if (!visible)
                return;
            Font original = GUI.skin.font;
            try
            {
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
                GUI.skin.font = font;
                window = GUI.Window(2784101, window, Draw, "PlateUp 游戏中转 · F8");
                if (!loggedDrawing && Event.current.type == EventType.Repaint)
                {
                    loggedDrawing = true;
                    Debug.Log("[PlateUpRelay] Settings window rendered.");
                }
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Close();
                Debug.LogError("[PlateUpRelay] Settings rendering failed: " + ex);
            }
            finally { GUI.skin.font = original; }
        }

        private void Draw(int id)
        {
            GUILayout.Label("服务器 IPv4（默认 UDP 27841，可填 IP:端口）");
            input = GUILayout.TextField(input, 64);
            GUILayout.Label("状态：" + RelaySettings.Status);
            bool busy = RelaySteamTransport.HasRemotePlayers;
            GUILayout.Label(busy ? "请先离开当前多人房间，再修改地址。" : "保存后正常邀请朋友；所有玩家填写同一个服务器。");
            bool original = GUI.enabled;
            GUI.enabled = !busy;
            if (GUILayout.Button("保存并启用"))
            {
                if (string.IsNullOrWhiteSpace(input))
                    error = "请输入服务器 IP。";
                else
                    Apply(input);
            }
            if (GUILayout.Button("关闭中转，使用原联机"))
                Apply("");
            GUI.enabled = original;
            if (!string.IsNullOrEmpty(error))
                GUILayout.Label(error);
            if (GUILayout.Button("关闭窗口"))
                Close();
            GUI.DragWindow(new Rect(0, 0, 540, 24));
        }

        private void Apply(string value)
        {
            try
            {
                if (RelaySteamTransport.HasRemotePlayers)
                    throw new InvalidOperationException("请先离开多人房间。");
                if (value != "" && string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("请输入服务器 IP。");
                RelaySettings.Save(value);
                input = RelaySettings.Address;
                error = "已保存";
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Debug.LogWarning("[PlateUpRelay] Settings: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            if (visible)
                Close();
            if (font != null)
                UnityEngine.Object.Destroy(font);
        }
    }
}
