@echo off
setlocal
chcp 65001 >nul
set "RelayInstallerFile=%~f0"
set "RelayPowerShell=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if defined PROCESSOR_ARCHITEW6432 set "RelayPowerShell=%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
"%RelayPowerShell%" -NoProfile -ExecutionPolicy Bypass -Command "$text=[IO.File]::ReadAllText($env:RelayInstallerFile); $marker=[regex]::Match($text,'(?m)^# POWERSHELL_START\r?$'); if(-not $marker.Success){exit 1}; & ([scriptblock]::Create($text.Substring($marker.Index+$marker.Length)))"
set "RelayExitCode=%errorlevel%"
echo.
pause
exit /b %RelayExitCode%
# POWERSHELL_START
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$serverAddress = '47.115.145.112'
$relayItem = [UInt64]3823021451
$harmonyItem = [UInt64]2898033283
$minimumVersion = [Version]'0.1.5.0'
$nativeAvailable = $false
$stage = '准备安装'

# 只加载已安装游戏自带的 Steam API；此脚本不下载或分发 Steam DLL。
$nativeSource = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

/// <summary>通过当前用户的 Steam 会话订阅、下载并获取工坊安装目录。</summary>
public static class RelaySteamInstaller
{
    private static IntPtr library;
    private static IntPtr ugc;
    private static IntPtr utils;
    private static bool initialized;
    private static int pipe;
    private static ulong downloadingItem;
    private static int? downloadResult;
    private const string SteamApi = "steam_api64";

    // Steamworks 平面 C 接口；结构对齐和版本取自游戏随附的 Facepunch 接口。
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct CallbackMessage
    {
        public int User;
        public int Type;
        public IntPtr Data;
        public int Size;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct DownloadResult
    {
        public uint AppId;
        public ulong FileId;
        public int Result;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    private struct SubscribeResult
    {
        public int Result;
        public ulong FileId;
    }

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_Init();
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_Shutdown();
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SteamAPI_GetHSteamPipe();
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_ManualDispatch_Init();
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_ManualDispatch_RunFrame(int pipe);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ManualDispatch_GetNextCallback(int pipe, ref CallbackMessage message);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SteamAPI_ManualDispatch_FreeLastCallback(int pipe);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamUGC_v014();
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SteamAPI_SteamUtils_v009();
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern ulong SteamAPI_ISteamUGC_SubscribeItem(IntPtr self, ulong item);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint SteamAPI_ISteamUGC_GetItemState(IntPtr self, ulong item);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamUGC_DownloadItem(IntPtr self, ulong item, [MarshalAs(UnmanagedType.I1)] bool highPriority);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamUGC_GetItemInstallInfo(IntPtr self, ulong item, out ulong size, IntPtr folder, uint folderSize, out uint timestamp);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamUtils_IsAPICallCompleted(IntPtr self, ulong call, [MarshalAs(UnmanagedType.I1)] out bool failed);
    [DllImport(SteamApi, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SteamAPI_ISteamUtils_GetAPICallResult(IntPtr self, ulong call, out SubscribeResult result, int size, int callback, [MarshalAs(UnmanagedType.I1)] out bool failed);

    /// <summary>只初始化工具进程的 Steam 会话，不启动游戏或修改 Steam 登录信息。</summary>
    public static void Initialise(string nativePath)
    {
        if (IntPtr.Size != 8)
            throw new InvalidOperationException("请使用 64 位 Windows PowerShell 运行此安装脚本。");
        library = LoadLibrary(nativePath);
        if (library == IntPtr.Zero)
            throw new InvalidOperationException("无法加载游戏的 Steam API，Windows 错误：" + Marshal.GetLastWin32Error());
        Environment.SetEnvironmentVariable("SteamAppId", "1599600");
        Environment.SetEnvironmentVariable("SteamGameId", "1599600");
        if (!SteamAPI_Init())
            throw new InvalidOperationException("Steam 初始化失败。请登录拥有 PlateUp 的 Steam 账号，并用同一 Windows 用户运行此脚本。");
        initialized = true;
        SteamAPI_ManualDispatch_Init();
        pipe = SteamAPI_GetHSteamPipe();
        ugc = SteamAPI_SteamUGC_v014();
        utils = SteamAPI_SteamUtils_v009();
        if (pipe == 0 || ugc == IntPtr.Zero || utils == IntPtr.Zero)
            throw new InvalidOperationException("游戏的 Steam 工坊接口不可用，请先检查游戏文件完整性。");
    }

    /// <summary>确保订阅；完整且无待更新内容时直接复用，否则等待下载完成。</summary>
    /// <param name="item">工坊物品 ID。</param>
    /// <param name="name">用于进度提示的模组名称。</param>
    /// <param name="requiredFiles">安装目录中必须存在的文件名。</param>
    /// <returns>Steam 提供且包含所需文件的安装目录。</returns>
    public static string Install(ulong item, string name, string[] requiredFiles)
    {
        if ((SteamAPI_ISteamUGC_GetItemState(ugc, item) & 1) == 0)
        {
            Console.WriteLine("正在订阅 " + name + "……");
            ulong call = SteamAPI_ISteamUGC_SubscribeItem(ugc, item);
            if (call == 0)
                throw new InvalidOperationException("Steam 无法发起订阅：" + item);
            Stopwatch subscription = Stopwatch.StartNew();
            bool failed;
            while (!SteamAPI_ISteamUtils_IsAPICallCompleted(utils, call, out failed))
            {
                if (subscription.Elapsed.TotalSeconds >= 60)
                    throw new TimeoutException("订阅超时，请检查 Steam 网络后重试：" + item);
                Pump();
                Thread.Sleep(100);
            }
            SubscribeResult result;
            if (failed || !SteamAPI_ISteamUtils_GetAPICallResult(utils, call, out result, Marshal.SizeOf(typeof(SubscribeResult)), 1313, out failed) || failed)
                throw new InvalidOperationException("Steam 未能返回有效订阅结果：" + item);
            if (result.Result != 1 || result.FileId != item)
                throw new InvalidOperationException("订阅失败：" + item + "，Steam 结果码：" + result.Result);
        }
        Pump();
        if (IsReady(item))
        {
            string installed = GetInstallDirectory(item);
            if (installed != null && Array.TrueForAll(requiredFiles, file => File.Exists(Path.Combine(installed, file))))
            {
                Console.WriteLine(name + " 已订阅且本地文件完整，直接使用。");
                return installed;
            }
        }
        Console.WriteLine("正在下载或更新 " + name + "……");
        downloadingItem = item;
        downloadResult = null;
        bool requested = SteamAPI_ISteamUGC_DownloadItem(ugc, item, true);
        if (!requested && !IsReady(item))
            throw new InvalidOperationException("Steam 无法开始下载，请检查下载列表后重试：" + item);
        Stopwatch download = Stopwatch.StartNew();
        while ((requested && !downloadResult.HasValue) || !IsReady(item))
        {
            if (download.Elapsed.TotalSeconds >= 300)
                throw new TimeoutException("下载超过五分钟。请在 Steam 中完成下载后重新运行脚本：" + item);
            Pump();
            if (downloadResult.HasValue && downloadResult.Value != 1)
                throw new InvalidOperationException("下载失败：" + item + "，Steam 结果码：" + downloadResult.Value);
            Thread.Sleep(250);
        }
        string folder = GetInstallDirectory(item);
        if (folder == null || !Array.TrueForAll(requiredFiles, file => File.Exists(Path.Combine(folder, file))))
            throw new InvalidOperationException(name + " 下载完成后所需文件仍不完整，请检查 Steam 下载列表。物品：" + item);
        return folder;
    }

    /// <summary>读取 Steam 的 UTF-8 安装路径；尚无安装信息或目录不存在时返回 null。</summary>
    /// <param name="item">工坊物品 ID。</param>
    /// <returns>已存在的安装目录，或 null。</returns>
    private static string GetInstallDirectory(ulong item)
    {
        IntPtr buffer = Marshal.AllocHGlobal(32768);
        try
        {
            ulong size;
            uint timestamp;
            if (!SteamAPI_ISteamUGC_GetItemInstallInfo(ugc, item, out size, buffer, 32768, out timestamp))
                return null;
            byte[] bytes = new byte[32768];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            int length = Array.IndexOf(bytes, (byte)0);
            if (length <= 0)
                throw new InvalidOperationException("Steam 返回的安装目录无效：" + item);
            string folder = Encoding.UTF8.GetString(bytes, 0, length);
            return Directory.Exists(folder) ? folder : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>等待明确的下载完成回调，避免把下载开始前的旧 Installed 状态当成完成。</summary>
    private static void Pump()
    {
        SteamAPI_ManualDispatch_RunFrame(pipe);
        CallbackMessage message = new CallbackMessage();
        while (SteamAPI_ManualDispatch_GetNextCallback(pipe, ref message))
        {
            try
            {
                if (message.Type == 3406 && message.Size == Marshal.SizeOf(typeof(DownloadResult)))
                {
                    DownloadResult result = (DownloadResult)Marshal.PtrToStructure(message.Data, typeof(DownloadResult));
                    if (result.AppId == 1599600 && result.FileId == downloadingItem)
                        downloadResult = result.Result;
                }
            }
            finally
            {
                SteamAPI_ManualDispatch_FreeLastCallback(pipe);
            }
        }
    }

    /// <summary>Subscribed、Installed 均为真，且没有 NeedsUpdate、Downloading、DownloadPending。</summary>
    private static bool IsReady(ulong item)
    {
        uint state = SteamAPI_ISteamUGC_GetItemState(ugc, item);
        return (state & 5) == 5 && (state & 56) == 0;
    }

    /// <summary>释放工具进程的接口与 DLL，不退出 Steam 客户端。</summary>
    public static void Shutdown()
    {
        if (initialized)
        {
            SteamAPI_Shutdown();
            initialized = false;
        }
        if (library != IntPtr.Zero)
        {
            FreeLibrary(library);
            library = IntPtr.Zero;
        }
    }
}
'@

# 按 Steam 库清单定位游戏，支持非默认盘符和中文路径。
function Find-GameDirectory([string]$steamRoot) {
    $libraries = New-Object 'System.Collections.Generic.List[string]'
    $libraries.Add($steamRoot)
    $libraryFile = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $libraryFile) {
        $libraryText = [IO.File]::ReadAllText($libraryFile)
        foreach ($entry in [regex]::Matches($libraryText, '"path"\s*"([^"]+)"')) {
            $libraries.Add($entry.Groups[1].Value.Replace('\\', '\'))
        }
    }
    foreach ($libraryPath in ($libraries | Select-Object -Unique)) {
        $manifest = Join-Path $libraryPath 'steamapps\appmanifest_1599600.acf'
        if (-not (Test-Path -LiteralPath $manifest)) { continue }
        $installMatch = [regex]::Match([IO.File]::ReadAllText($manifest), '"installdir"\s*"([^"]+)"')
        if (-not $installMatch.Success) { continue }
        $gameDirectory = Join-Path $libraryPath ('steamapps\common\' + $installMatch.Groups[1].Value + '\PlateUp')
        if (Test-Path -LiteralPath (Join-Path $gameDirectory 'PlateUp.exe')) { return $gameDirectory }
    }
    throw '没有在 Steam 库中找到 PlateUp。请先在当前 Steam 账号中安装游戏。'
}

function Assert-GameClosed {
    if (Get-Process -Name 'PlateUp' -ErrorAction SilentlyContinue) {
        throw '请先完全退出 PlateUp，再运行安装脚本；脚本不会强制关闭游戏。'
    }
}

try {
    Write-Host 'PlateUp 群友中转安装器' -ForegroundColor Cyan
    Write-Host ('服务器：' + $serverAddress)
    $stage = '定位 Steam 和游戏目录'
    Assert-GameClosed
    $steamEntry = Get-ItemProperty -LiteralPath 'HKCU:\Software\Valve\Steam' -Name SteamPath -ErrorAction SilentlyContinue
    if ($null -eq $steamEntry -or [string]::IsNullOrWhiteSpace($steamEntry.SteamPath)) {
        throw '没有找到当前 Windows 用户的 Steam 安装信息。请先启动并登录 Steam。'
    }
    $steamRoot = $steamEntry.SteamPath
    $gameDirectory = Find-GameDirectory $steamRoot
    $nativePath = Join-Path $gameDirectory 'PlateUp_Data\Plugins\x86_64\steam_api64.dll'
    if (-not (Test-Path -LiteralPath $nativePath)) { throw ('缺少游戏文件：' + $nativePath) }
    if (-not (Get-Process -Name steam -ErrorAction SilentlyContinue)) {
        Start-Process -FilePath (Join-Path $steamRoot 'steam.exe')
        [void](Read-Host '请在 Steam 中完成登录，然后按回车继续')
    }
    Write-Host ('游戏目录：' + $gameDirectory)
    $stage = '连接 Steam 工坊接口'
    Add-Type -TypeDefinition $nativeSource -Language CSharp
    $nativeAvailable = $true
    [RelaySteamInstaller]::Initialise($nativePath)
    $stage = '准备 Harmony 前置模组'
    $harmonyDirectory = [RelaySteamInstaller]::Install($harmonyItem, 'Harmony', [string[]]@('0Harmony.dll'))
    if (-not (Test-Path -LiteralPath (Join-Path $harmonyDirectory '0Harmony.dll'))) {
        throw 'Harmony 下载目录中没有 0Harmony.dll，请检查工坊下载。'
    }
    $stage = '准备游戏中转模组'
    $requiredFiles = @('PlateUpRelay.dll', 'LiteNetLib.dll', 'LiteNetLib-LICENSE.txt')
    $downloadDirectory = [RelaySteamInstaller]::Install($relayItem, '游戏中转模组', [string[]]$requiredFiles)
    foreach ($fileName in $requiredFiles) {
        if (-not (Test-Path -LiteralPath (Join-Path $downloadDirectory $fileName))) {
            throw ('工坊下载缺少文件：' + $fileName + '。请联系模组发布者。')
        }
    }
    Write-Host ('工坊模组目录：' + $downloadDirectory)
    $stage = '检查模组版本'
    $modVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $downloadDirectory 'PlateUpRelay.dll')).Version
    if ($modVersion -lt $minimumVersion) {
        throw ('工坊下载的版本是 ' + $modVersion + '，此脚本要求至少 ' + $minimumVersion + '。请等待管理员上传新版；本地文件尚未修改。')
    }
    $stage = '准备本地模组与服务器配置路径'
    Assert-GameClosed
    $modsDirectory = Join-Path $gameDirectory 'Mods'
    $targetDirectory = Join-Path $modsDirectory 'PlateUpRelay'
    $targetDll = Join-Path $targetDirectory 'PlateUpRelay.dll'
    if (Test-Path -LiteralPath $modsDirectory) {
        foreach ($existing in Get-ChildItem -LiteralPath $modsDirectory -Filter PlateUpRelay.dll -File -Recurse) {
            if (-not $existing.FullName.Equals($targetDll, [StringComparison]::OrdinalIgnoreCase)) {
                throw ('发现其他位置的中转模组，请先移到 Mods 目录之外以避免重复加载：' + $existing.FullName)
            }
        }
    }
    $localLow = Join-Path $env:USERPROFILE 'AppData\LocalLow'
    $folderKey = '{A520A1A4-1780-4FF6-BD18-167343C5AF16}'
    $shellFolders = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders'
    if ($null -ne $shellFolders.PSObject.Properties[$folderKey]) {
        $localLow = [Environment]::ExpandEnvironmentVariables($shellFolders.$folderKey)
    }
    $configDirectory = Join-Path $localLow "It's Happening\PlateUp\PlateUpRelay"
    $configPath = Join-Path $configDirectory 'server.txt'
    $stamp = (Get-Date -Format yyyyMMdd-HHmmss) + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    # 备份与 Mods 位于同一磁盘，目录移动可回滚；此目录不会被游戏当作模组加载。
    $backupDirectory = Join-Path $gameDirectory ('PlateUpRelay.Backups\' + $stamp)
    $stageDirectory = Join-Path $gameDirectory ('.PlateUpRelay-install-' + $stamp)
    Write-Host ('本地模组：' + $targetDirectory)
    Write-Host ('服务器配置：' + $configPath)
    Write-Host ('备份目录：' + $backupDirectory)
    [void][IO.Directory]::CreateDirectory($stageDirectory)
    [void][IO.Directory]::CreateDirectory($backupDirectory)
    [void][IO.Directory]::CreateDirectory($modsDirectory)
    [void][IO.Directory]::CreateDirectory($configDirectory)
    $configTemporary = Join-Path $configDirectory ('server.txt.install-' + $stamp)
    $oldMoved = $false
    $newInstalled = $false
    try {
        $stage = '复制并校验模组文件'
        foreach ($fileName in $requiredFiles) {
            $sourcePath = Join-Path $downloadDirectory $fileName
            $stagedPath = Join-Path $stageDirectory $fileName
            Copy-Item -LiteralPath $sourcePath -Destination $stagedPath
            if ((Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $stagedPath -Algorithm SHA256).Hash) {
                throw ('复制校验失败：' + $fileName)
            }
        }
        $stage = '备份配置并准备服务器地址'
        if (Test-Path -LiteralPath $configPath) {
            Copy-Item -LiteralPath $configPath -Destination (Join-Path $backupDirectory 'server.txt')
        }
        [IO.File]::WriteAllText($configTemporary, $serverAddress, (New-Object System.Text.UTF8Encoding($false)))
        Assert-GameClosed
        $stage = '替换本地模组'
        if (Test-Path -LiteralPath $targetDirectory) {
            Move-Item -LiteralPath $targetDirectory -Destination (Join-Path $backupDirectory 'PlateUpRelay')
            $oldMoved = $true
        }
        Move-Item -LiteralPath $stageDirectory -Destination $targetDirectory
        $newInstalled = $true
        $stage = '保存服务器配置'
        # 原子替换配置是最后的提交步骤；失败时恢复之前的模组目录。
        if (Test-Path -LiteralPath $configPath) {
            # 用 NullString 传递真正的 null；PowerShell 的 $null 会被转换成空路径。
            [IO.File]::Replace($configTemporary, $configPath, [System.Management.Automation.Language.NullString]::Value)
        } else {
            [IO.File]::Move($configTemporary, $configPath)
        }
    } catch {
        $installationError = $_
        try {
            if ($newInstalled) {
                Move-Item -LiteralPath $targetDirectory -Destination (Join-Path $backupDirectory 'failed-install')
            }
            if ($oldMoved) {
                Move-Item -LiteralPath (Join-Path $backupDirectory 'PlateUpRelay') -Destination $targetDirectory
            }
            if (Test-Path -LiteralPath $stageDirectory) {
                Move-Item -LiteralPath $stageDirectory -Destination (Join-Path $backupDirectory 'staging')
            }
            if (Test-Path -LiteralPath $configTemporary) {
                Move-Item -LiteralPath $configTemporary -Destination (Join-Path $backupDirectory 'pending-server.txt')
            }
        } catch {
            Write-Warning ('自动恢复未完成，请保留此备份目录：' + $backupDirectory + '。' + $_.Exception.Message)
        }
        throw $installationError
    }
    Write-Host ('安装完成，模组版本：' + $modVersion) -ForegroundColor Green
    Write-Host ('本地模组：' + $targetDirectory)
    Write-Host ('服务器配置：' + $configPath + ' = ' + $serverAddress)
    Write-Host ('旧文件备份：' + $backupDirectory)
    Write-Host '请启动游戏，通过 Steam 邀请或加入房间；右下角显示 Server / Room 延迟。'
    Write-Host '需要修改地址时，进入“设置 → 游戏中转”。工坊副本保留订阅；本地副本更新时重新运行此脚本。'
} catch {
    Write-Host ('安装未完成：' + $_.Exception.GetBaseException().Message) -ForegroundColor Red
    Write-Host ('失败步骤：' + $stage) -ForegroundColor Yellow
    Write-Host $_.InvocationInfo.PositionMessage
    Write-Host $_.ScriptStackTrace
    exit 1
} finally {
    if ($nativeAvailable) { [RelaySteamInstaller]::Shutdown() }
}
