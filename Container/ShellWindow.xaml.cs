using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Container
{
    public partial class ShellWindow : Window
    {
        private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("SHELLPACK_V1");

        public ShellWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            var (target, html) = ReadSelf();

            if (target == null || html == null)
            {
                MessageBox.Show("无壳数据");
                Application.Current.Shutdown();
                return;
            }

            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.AddHostObjectToScript("app", new Bridge(target));
            WebView.CoreWebView2.NavigateToString(html);
        }

        private static (byte[]? target, string? html) ReadSelf()
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
    }

    [System.Runtime.InteropServices.ComVisible(true)]
    public class Bridge
    {
        private readonly byte[] _target;
        public Bridge(byte[] target) { _target = target; }

        public void Launch()
        {
            string tmpDir = Path.Combine(Path.GetTempPath(),
                "ShellApp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            string tmpExe = Path.Combine(tmpDir,
                OperatingSystem.IsWindows() ? "target.exe" : "target");
            File.WriteAllBytes(tmpExe, _target);

            if (!OperatingSystem.IsWindows())
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

            Application.Current.Shutdown();
        }
    }
}
