using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Photino.NET;

namespace Container
{
    class Program
    {
        static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("SHELLPACK_V1");

        [STAThread]
        static void Main(string[] args)
        {
            // 1. Windows 上先检查 WebView2
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsWebView2Installed())
            {
                if (!TryInstallWebView2())
                {
                    Console.WriteLine("WebView2 安装失败或用户取消");
                    return;
                }
            }

            var (targetBytes, html, wv2Unused, width, height) = ReadSelf();

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

        // 检查注册表是否有 WebView2
        static bool IsWebView2Installed()
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
                using var k1 = hklm.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                if (k1 != null) return true;

                using var hkcu = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32);
                using var k2 = hkcu.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}");
                if (k2 != null) return true;
            }
            catch { }
            return false;
        }

        // 释放并静默安装 WebView2
        static bool TryInstallWebView2()
        {
            try
            {
                var (_, _, wv2Bytes, _, _) = ReadSelf();
                if (wv2Bytes == null || wv2Bytes.Length == 0)
                    return false;

                string tmp = Path.Combine(Path.GetTempPath(),
                    "WebView2Setup_" + Guid.NewGuid().ToString("N") + ".exe");
                File.WriteAllBytes(tmp, wv2Bytes);

                var psi = new ProcessStartInfo
                {
                    FileName = tmp,
                    Arguments = "/silent /install",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                p.WaitForExit();

                try { File.Delete(tmp); } catch { }

                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        static (byte[]? target, string? html, byte[]? wv2, int width, int height) ReadSelf()
        {
            string self = Process.GetCurrentProcess().MainModule!.FileName;
            if (!File.Exists(self)) return (null, null, null, 900, 600);

            byte[] all = File.ReadAllBytes(self);
            int total = all.Length;

            // 末尾 44 字节：[MAGIC 12][tLen 8][hLen 8][wv2Len 8][w 4][h 4]
            if (total < 44) return (null, null, null, 900, 600);

            int magicPos = total - 44;
            for (int i = 0; i < 12; i++)
                if (all[magicPos + i] != MAGIC[i]) return (null, null, null, 900, 600);

            long tLen = BitConverter.ToInt64(all, magicPos + 12);
            long hLen = BitConverter.ToInt64(all, magicPos + 20);
            long wLen = BitConverter.ToInt64(all, magicPos + 28);
            int w = BitConverter.ToInt32(all, magicPos + 36);
            int h = BitConverter.ToInt32(all, magicPos + 40);

            if (tLen <= 0 || hLen < 0 || wLen < 0) return (null, null, null, 900, 600);
            if (44 + tLen + hLen + wLen > total) return (null, null, null, 900, 600);

            if (w < 100 || w > 10000) w = 900;
            if (h < 100 || h > 10000) h = 600;

            long dataStart = total - 44 - tLen - hLen - wLen;

            byte[] targetBytes = new byte[tLen];
            Array.Copy(all, dataStart, targetBytes, 0, tLen);

            byte[] htmlBytes = new byte[hLen];
            Array.Copy(all, dataStart + tLen, htmlBytes, 0, hLen);

            byte[] wv2Bytes = new byte[wLen];
            if (wLen > 0)
                Array.Copy(all, dataStart + tLen + hLen, wv2Bytes, 0, wLen);

            return (targetBytes, Encoding.UTF8.GetString(htmlBytes), wv2Bytes, w, h);
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
