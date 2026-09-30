using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShellContainer
{
    internal static class WebView2Runtime
    {
        private static readonly Guid ClientId = new("F3017226-FE2A-4295-8BDF-00C3A9A7E4C5");

        public static bool IsInstalled()
        {
            try
            {
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey($@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{{{ClientId}}}"))
                {
                    if (k != null) return true;
                }
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                    .OpenSubKey($@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{{{ClientId}}}"))
                {
                    if (k != null) return true;
                }
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                    .OpenSubKey($@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{{{ClientId}}}"))
                {
                    if (k != null) return true;
                }
                using (var k = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default)
                    .OpenSubKey($@"Software\Microsoft\EdgeUpdate\Clients\{{{ClientId}}}"))
                {
                    if (k != null) return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 确保 WebView2 可用。没有 → 提示用户 → 用户范围静默安装 → 重启容器。
        /// </summary>
        public static bool EnsureInstalled()
        {
            if (IsInstalled()) return true;

            // 读壳数据里的安装器
            Payload payload;
            try { payload = Payload.ReadFromSelf(); }
            catch { return false; }

            if (payload.Wv2InstallerLen <= 0)
            {
                MessageBox.Show(
                    "缺少 WebView2 运行时，且程序未内嵌安装器。\n" +
                    "请手动下载安装：\n" +
                    "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                    "缺少组件");
                return false;
            }

            var result = MessageBox.Show(
                "首次运行需要安装 Microsoft Edge WebView2 运行时。\n\n" +
                "点击【确定】自动安装（约 1~2 分钟，请勿关闭程序）。\n" +
                "点击【取消】退出手动安装。",
                "正在准备运行环境",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);

            if (result != DialogResult.OK) return false;

            try
            {
                byte[] installer = payload.ReadInstaller();

                string tempDir = Path.Combine(Path.GetTempPath(),
                    "shellpack_wv2_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                string instPath = Path.Combine(tempDir, "MicrosoftEdgeWebview2Setup.exe");
                File.WriteAllBytes(instPath, installer);

                // ★ 用户范围静默安装，不需要管理员
                var psi = new ProcessStartInfo(instPath,
                    "/silent /install /installerscope=user")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit();

                try { File.Delete(instPath); } catch { }
                try { Directory.Delete(tempDir, true); } catch { }

                if (IsInstalled())
                {
                    // 安装成功，重启容器
                    string self = Environment.ProcessPath!;
                    Process.Start(new ProcessStartInfo(self) { UseShellExecute = true });
                    Environment.Exit(0);
                    return false;
                }

                MessageBox.Show(
                    "WebView2 安装失败，请手动安装：\n" +
                    "https://go.microsoft.com/fwlink/p/?LinkId=2124703",
                    "安装失败");
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("安装 WebView2 失败：" + ex.Message);
                return false;
            }
        }
    }
}
