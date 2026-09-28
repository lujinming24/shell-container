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
            var (targetBytes, html, width, height) = ReadSelf();

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

            string finalHtml = html + @"
<script>
if (typeof launch !== 'function') {
    function launch() {
        window.external.sendMessage('launch');
    }
}
</script>";

            var window = new PhotinoWindow()
                .SetTitle("Loading")
                .SetUseOsDefaultSize(false)
                .SetSize(width, height)
                .Center()
                .RegisterWebMessageReceivedHandler((sender, message) =>
                {
                    LaunchTarget(targetBytes);
                });

            window.LoadRawString(finalHtml);
            window.WaitForClose();
        }

        static (byte[]? target, string? html, int width, int height) ReadSelf()
        {
            string self = Process.GetCurrentProcess().MainModule!.FileName;
            if (!File.Exists(self)) return (null, null, 900, 600);

            byte[] all = File.ReadAllBytes(self);
            int total = all.Length;

            if (total < 36) return (null, null, 900, 600);

            int magicPos = total - 36;
            for (int i = 0; i < 12; i++)
                if (all[magicPos + i] != MAGIC[i]) return (null, null, 900, 600);

            long tLen = BitConverter.ToInt64(all, magicPos + 12);
            long hLen = BitConverter.ToInt64(all, magicPos + 20);
            int w = BitConverter.ToInt32(all, magicPos + 28);
            int h = BitConverter.ToInt32(all, magicPos + 32);

            if (tLen <= 0 || hLen < 0) return (null, null, 900, 600);
            if (36 + tLen + hLen > total) return (null, null, 900, 600);

            if (w < 100 || w > 10000) w = 900;
            if (h < 100 || h > 10000) h = 600;

            long dataStart = total - 36 - tLen - hLen;

            byte[] targetBytes = new byte[tLen];
            Array.Copy(all, dataStart, targetBytes, 0, tLen);

            byte[] htmlBytes = new byte[hLen];
            Array.Copy(all, dataStart + tLen, htmlBytes, 0, hLen);

            return (targetBytes, Encoding.UTF8.GetString(htmlBytes), w, h);
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
