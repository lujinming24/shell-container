using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Photino.NET;

namespace Container
{
    class Program
    {
        static Stopwatch _sw = new Stopwatch();

        static void Log(string msg)
        {
          try
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "container_timing.log");
        File.AppendAllText(path,
            DateTime.Now.ToString("HH:mm:ss.fff") +
            "  +" + _sw.ElapsedMilliseconds + "ms  " +
            msg + "\r\n");
    }
    catch { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            // 清空旧日志
            try { File.Delete(@"C:\container_timing.log"); } catch { }

            _sw.Start();
            Log("=== 程序开始 ===");

            Log("开始读 payload");
            var payload = Payload.ReadSelf();
            Log("payload 读取完成 target=" + (payload.target?.Length ?? 0) +
                " html=" + (payload.html?.Length ?? 0) +
                " logic=" + (payload.logicDll?.Length ?? 0) +
                " wv2=" + (payload.wv2?.Length ?? 0));

            if (payload.target == null || payload.html == null)
            {
                Log("无壳数据，弹提示窗口");
                var w = new PhotinoWindow()
                    .SetTitle("Container")
                    .SetUseOsDefaultSize(false)
                    .SetSize(400, 200)
                    .Center();
                w.SetLogVerbosity(0);
                w.LoadRawString(
                    "<h1 style='font-family:sans-serif;text-align:center;" +
                    "padding-top:40px'>无壳数据</h1>");
                Log("无壳数据窗口已创建，等关闭");
                w.WaitForClose();
                Log("无壳数据窗口关闭");
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsWebView2Installed())
            {
                Log("WebView2 未安装，开始安装");
                if (!TryInstallWebView2(payload.wv2))
                {
                    Log("WebView2 安装失败");
                    return;
                }
                Log("WebView2 安装完成");
            }
            else
            {
                Log("WebView2 已安装或非 Windows 平台");
            }

            Log("开始加载 Logic.dll");
            var logicHost = new LogicHost();
            bool logicLoaded = logicHost.Load(payload.logicDll);
            Log("Logic.dll 加载完成 loaded=" + logicLoaded);

            string injectScript = @"
<script>
function send(action, data) {
    var obj = { action: action };
    if (data) for (var k in data) obj[k] = data[k];
    window.external.sendMessage(JSON.stringify(obj));
}
if (window.external && window.external.receiveMessage) {
    window.external.receiveMessage(function(msg) {
        try { eval(msg); } catch (e) { console.error(e); }
    });
}
</script>";

            string finalHtml = payload.html + injectScript;
            Log("准备创建 Photino 窗口");

            PhotinoWindow? window = null;

            window = new PhotinoWindow()
                .SetTitle(payload.title)
                .SetUseOsDefaultSize(false)
                .SetSize(payload.width, payload.height)
                .Center()
                .RegisterWebMessageReceivedHandler((sender, message) =>
                {
                    if (!logicLoaded || window == null) return;

                    string js = logicHost.Handle(
                        message,
                        executeJs: (s) => { try { window.SendWebMessage(s); } catch { } },
                        launchTarget: () => LaunchTarget(payload.target!),
                        exitApp: () => Environment.Exit(0));

                    if (!string.IsNullOrEmpty(js))
                    {
                        try { window.SendWebMessage(js); } catch { }
                    }
                });

            Log("PhotinoWindow 构造完成（链式调用结束）");

            window.SetLogVerbosity(0);
            Log("SetLogVerbosity 完成");

            Log("准备 LoadRawString");
            window.LoadRawString(finalHtml);
            Log("LoadRawString 完成");

            Log("准备进入 WaitForClose");
            window.WaitForClose();
            Log("=== 窗口关闭，程序退出 ===");
        }

        [SupportedOSPlatform("windows")]
        static bool IsWebView2Installed()
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var k1 = hklm.OpenSubKey(
                    @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                if (k1 != null) return true;

                using var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
                using var k2 = hkcu.OpenSubKey(
                    @"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                if (k2 != null) return true;
            }
            catch { }
            return false;
        }

        [SupportedOSPlatform("windows")]
        static bool TryInstallWebView2(byte[]? wv2)
        {
            try
            {
                if (wv2 == null || wv2.Length == 0) return false;

                string tmp = Path.Combine(Path.GetTempPath(),
                    "WebView2Setup_" + Guid.NewGuid().ToString("N") + ".exe");
                File.WriteAllBytes(tmp, wv2);

                var psi = new ProcessStartInfo
                {
                    FileName = tmp,
                    Arguments = "/silent /install",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit();

                try { File.Delete(tmp); } catch { }
                return proc != null && proc.ExitCode == 0;
            }
            catch { return false; }
        }

        static void LaunchTarget(byte[] targetBytes)
        {
            string tmpDir = Path.Combine(Path.GetTempPath(),
                "ShellApp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            string tmpExe = Path.Combine(tmpDir,
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "target.exe" : "target");

            File.WriteAllBytes(tmpExe, targetBytes);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    var psi = new ProcessStartInfo("chmod", $"+x \"{tmpExe}\"")
                    { UseShellExecute = false, CreateNoWindow = true };
                    Process.Start(psi)?.WaitForExit();
                }
                catch { }
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = tmpExe,
                WorkingDirectory = tmpDir,
                UseShellExecute = true
            });

            Environment.Exit(0);
        }
    }
}
