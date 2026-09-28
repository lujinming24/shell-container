using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Photino.NET;

namespace Container
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            PhotinoWindow.SetLogVerbosity(0);

            var payload = Payload.ReadSelf();

            if (payload.target == null || payload.html == null)
            {
                var w = new PhotinoWindow()
                    .SetTitle("Container")
                    .SetUseOsDefaultSize(false)
                    .SetSize(400, 200)
                    .Center()
                    .LoadRawString(
                        "<h1 style='font-family:sans-serif;text-align:center;" +
                        "padding-top:40px'>无壳数据</h1>");
                w.WaitForClose();
                return;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsWebView2Installed())
            {
                if (!TryInstallWebView2(payload.wv2))
                    return;
            }

            var logicHost = new LogicHost();
            bool logicLoaded = logicHost.Load(payload.logicDll);

            string injectScript = @"
<script>
function send(action, data) {
    var obj = { action: action };
    if (data) for (var k in data) obj[k] = data[k];
    window.external.sendMessage(JSON.stringify(obj));
}
</script>";

            string finalHtml = payload.html + injectScript;

            var window = new PhotinoWindow()
                .SetTitle(payload.title)
                .SetUseOsDefaultSize(false)
                .SetSize(payload.width, payload.height)
                .Center()
                .RegisterWebMessageReceivedHandler((sender, message) =>
                {
                    if (!logicLoaded) return;

                    string js = logicHost.Handle(
                        message,
                        executeJs: (s) => { try { window.ExecuteScript(s); } catch { } },
                        launchTarget: () => LaunchTarget(payload.target!),
                        exitApp: () => Environment.Exit(0));

                    if (!string.IsNullOrEmpty(js))
                    {
                        try { window.ExecuteScript(js); } catch { }
                    }
                });

            window.LoadRawString(finalHtml);
            window.WaitForClose();
        }

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
