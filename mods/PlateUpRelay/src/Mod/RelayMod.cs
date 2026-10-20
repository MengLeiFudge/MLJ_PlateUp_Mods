using System;
using System.IO;
using System.Net;
using System.Text;
using HarmonyLib;
using Kitchen;
using Kitchen.NetworkSupport;
using Kitchen.Transports;
using KitchenMods;
using UnityEngine;

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
            // 菜单与 HUD 通过游戏自身的创建时机接入，不在预加载阶段创建 UI。
            loaded = true;
            Debug.Log("[PlateUpRelay] v0.1.5 loaded; settings menu and latency HUD registered.");
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
        /// <summary>安装脚本与游戏菜单共用的配置文件；空内容表示关闭中转。</summary>
        internal static string ConfigurationPath => Path.Combine(Application.persistentDataPath, "PlateUpRelay", "server.txt");
        public static string Address { get; private set; } = "";
        public static IPEndPoint Endpoint { get; private set; }
        public static int Revision { get; private set; }
        public static bool Enabled => Endpoint != null;
        public static RelayText Status = RelayText.Disabled;

        /// <summary>优先读取配置文件；没有文件时兼容旧 PlayerPrefs，保存后以文件为准。</summary>
        public static void Load()
        {
            try
            {
                string saved = File.Exists(ConfigurationPath)
                    ? File.ReadAllText(ConfigurationPath).Trim()
                    : PlayerPrefs.GetString(PreferenceKey, "").Trim();
                if (!TryParse(saved, out var endpoint))
                    throw new ArgumentException(RelayText.InvalidAddress.Value);
                Address = saved;
                Endpoint = endpoint;
                Status = Enabled ? RelayText.WaitingLobby : RelayText.Disabled;
            }
            catch (Exception ex)
            {
                Status = RelayText.Failure(ex.Message);
                Debug.LogError("[PlateUpRelay] Cannot load settings from " + ConfigurationPath + ": " + ex);
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

        /// <summary>持久化地址并刷新待连接状态；传输层在 Steam 房间就绪后处理新的配置修订。</summary>
        /// <param name="text">服务器地址；空字符串表示关闭中转。</param>
        public static void Save(string text)
        {
            if (RelaySteamTransport.HasRemotePlayers)
                throw new InvalidOperationException(RelayText.LeaveLobby.Value);
            if (!TryParse(text, out var endpoint))
                throw new ArgumentException(RelayText.InvalidAddress.Value);
            string normalized = text.Trim();
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigurationPath));
            string temporary = ConfigurationPath + ".tmp";
            File.WriteAllText(temporary, normalized, new UTF8Encoding(false));
            // 先完成磁盘替换再改变运行时状态，避免写入失败时误报启用成功。
            if (File.Exists(ConfigurationPath))
                File.Replace(temporary, ConfigurationPath, null);
            else
                File.Move(temporary, ConfigurationPath);
            Address = normalized;
            Endpoint = endpoint;
            Revision++;
            Status = Enabled ? RelayText.WaitingLobby : RelayText.Disabled;
            Debug.Log(Enabled
                ? "[PlateUpRelay] Settings saved; relay enabled for " + Address + ". Waiting for a ready Steam lobby."
                : "[PlateUpRelay] Settings saved; relay disabled.");
        }
    }
}
