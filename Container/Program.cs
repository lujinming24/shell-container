using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Photino.NET;

namespace Container
{
    class Program
    {
        static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("SHELLPACK_V1");

        [STAThread]
        static void Main(string[] args)
        {
            var (targetBytes, html) = ReadSelf();

            if (targetBytes == null || html == null)
            {
                var w = new PhotinoWindow()
                    .SetTitle("Container")
                    .SetUseOsDefaultSize(false)
                    .SetSize(400, 200)
                    .Center()
                    .LoadRawString("<h1 style='font-family:sans-serif;text-align:center;padding-top:40px'>无壳数据</h1>");
                w.WaitForClose();
                return;
            }

            // 在 HTML 末尾注入 launch 函数
            string finalHtml = html + @"
<script>
function launch() {
    window.external.sendMessage('launch');
}
</script>";

            var window = new PhotinoWindow()
                .SetTitle("Loading")
                .SetUseOsDefaultSize(true)
                .SetFullScreen(true)
                .RegisterWebMessageReceivedHandler((sender, message) =>
                {
                    LaunchTarget(targetBytes);
                });

            window.LoadRawString(finalHtml);
            window.WaitForClose();
        }

        static (byte[]? target, string? html) ReadSelf()
        {
            string self = Process.GetCurrentProcess().MainModule!.FileName;
            if (!File.Exists(self)) return (null, null);

            byte[] all = File.ReadAllBytes(self);
            int total = all.Length;

            if (total < 28) return (null, null);

            int magicPos = total - 28;
            for (int i = 0; i < 12; i++)
                if (all[magicPos + i] != MAGIC[i]) return (null, null);

            long tLen = BitConverter.ToInt64(all, magicPos + 12);
            long hLen = BitConverter.ToInt64(all, magicPos + 20);

            if (tLen <= 0 || hLen < 0) return (null, null);
            if (28 + tLen + hLen > total) return (null, null);

            long dataStart = total - 28 - tLen - hLen;

            byte[] targetBytes = new byte[tLen];
            Array.Copy(all, dataStart, targetBytes, 0, tLen);

            byte[] htmlBytes = new byte[hLen];
            Array.Copy(all, dataStart + tLen, htmlBytes, 0, hLen);

            return (targetBytes, Encoding.UTF8.GetString(htmlBytes));
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
