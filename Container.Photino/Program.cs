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
        [STAThread]
        static void Main(string[] args)
        {
            var payload = Payload.ReadSelf();

            if (payload.target == null || payload.html == null)
            {
                var w = new PhotinoWindow()
                    .SetTitle("Container")
                    .SetUseOsDefaultSize(false)
                    .SetSize(400, 200)
                    .Center();
                w.SetLogVerbosity(0);
                w.LoadRawString("<h1 style='font-family:sans-serif;text-align:center;padding-top:40px'>无壳数据</h1>");
                w.WaitForClose();
                return;
            }

            // 加载 Logic.dll
            var logicHost = new LogicHost();
            bool logicLoaded = logicHost.Load(payload.logicDll);

            // 注入脚本
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

            window.SetLogVerbosity(0);
            window.LoadRawString(finalHtml);
            window.WaitForClose();
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
