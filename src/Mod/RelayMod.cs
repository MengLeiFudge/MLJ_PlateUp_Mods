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
        public static RelayText Status = RelayText.Disabled;

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
                throw new ArgumentException(RelayText.InvalidAddress.Value);
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
        /// <summary>面板布局使用的参考分辨率；绘制时按游戏渲染尺寸等比缩放。</summary>
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private bool visible;
        private string input = "";
        private RelayText error;
        private Rect window = new Rect(40, 80, 760, 560);
        private Font font;
        /// <summary>面板私有皮肤；放大控件时不改动游戏共享样式，随面板销毁。</summary>
        private GUISkin skin;
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
                    error = default;
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
            GUISkin original = GUI.skin;
            Matrix4x4 originalMatrix = GUI.matrix;
            try
            {
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 22);
                if (skin == null)
                {
                    skin = UnityEngine.Object.Instantiate(original);
                    skin.font = font;
                    skin.label.font = font;
                    skin.label.fontSize = 22;
                    skin.label.wordWrap = true;
                    skin.label.fixedHeight = 0;
                    skin.textField.font = font;
                    skin.textField.fontSize = 22;
                    skin.textField.fixedHeight = 44;
                    skin.textField.padding = new RectOffset(12, 12, 8, 8);
                    skin.button.font = font;
                    skin.button.fontSize = 22;
                    skin.button.fixedHeight = 44;
                    skin.window.font = font;
                    skin.window.fontSize = 24;
                    skin.window.padding = new RectOffset(24, 24, 48, 24);
                }
                // 使用同一矩阵缩放绘制与 IMGUI 输入坐标；分辨率变化后将窗口留在屏幕内。
                float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
                window.x = Mathf.Clamp(window.x, 0, Screen.width / scale - window.width);
                window.y = Mathf.Clamp(window.y, 0, Screen.height / scale - window.height);
                GUI.matrix = originalMatrix * Matrix4x4.Scale(new Vector3(scale, scale, 1));
                GUI.skin = skin;
                window = GUI.Window(2784101, window, Draw, RelayText.Title.Value);
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
            finally
            {
                GUI.skin = original;
                GUI.matrix = originalMatrix;
            }
        }

        private void Draw(int id)
        {
            GUILayout.Label(RelayText.ServerRequired.Value);
            GUILayout.Label(RelayText.PortNotEnough.Value);
            GUILayout.Label(RelayText.AddressLabel.Value);
            input = GUILayout.TextField(input, 64);
            GUILayout.Label(RelayText.StatusLabel.Value + RelaySettings.Status.Value);
            bool busy = RelaySteamTransport.HasRemotePlayers;
            GUILayout.Label((busy ? RelayText.LeaveLobbyToChange : RelayText.InviteHint).Value);
            bool original = GUI.enabled;
            GUI.enabled = !busy;
            if (GUILayout.Button(RelayText.SaveAndEnable.Value))
            {
                if (string.IsNullOrWhiteSpace(input))
                    error = RelayText.EnterAddress;
                else
                    Apply(input);
            }
            if (GUILayout.Button(RelayText.DisableRelay.Value))
                Apply("");
            GUI.enabled = original;
            if (!string.IsNullOrEmpty(error.Value))
                GUILayout.Label(error.Value);
            if (GUILayout.Button(RelayText.CloseWindow.Value))
                Close();
            GUI.DragWindow(new Rect(0, 0, window.width, 40));
        }

        private void Apply(string value)
        {
            try
            {
                if (RelaySteamTransport.HasRemotePlayers)
                    throw new InvalidOperationException(RelayText.LeaveLobby.Value);
                if (value != "" && string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException(RelayText.EnterAddress.Value);
                RelaySettings.Save(value);
                input = RelaySettings.Address;
                error = RelayText.Saved;
            }
            catch (Exception ex)
            {
                error = new RelayText(ex.Message, ex.Message);
                Debug.LogWarning("[PlateUpRelay] Settings: " + ex.Message);
            }
        }

        private void OnDestroy()
        {
            if (visible)
                Close();
            if (skin != null)
                UnityEngine.Object.Destroy(skin);
            if (font != null)
                UnityEngine.Object.Destroy(font);
        }
    }
}
