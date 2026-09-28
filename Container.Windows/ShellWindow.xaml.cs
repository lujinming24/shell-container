using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Container
{
    public partial class ShellWindow : Window
    {
        private readonly CoreWebView2Environment _env;
        private readonly Payload _payload;
        private readonly LogicHost _logicHost;
        private readonly bool _logicLoaded;

        public ShellWindow(CoreWebView2Environment env, Payload payload,
                            LogicHost logicHost, bool logicLoaded)
        {
            InitializeComponent();

            _env = env;
            _payload = payload;
            _logicHost = logicHost;
            _logicLoaded = logicLoaded;

            Loaded += ShellWindow_Loaded;
        }

        private async void ShellWindow_Loaded(object sender, RoutedEventArgs e)
        {
            App.Log("ShellWindow.Loaded 触发");

            // 用已经热好的环境初始化 WebView2 控件
            App.Log("开始 EnsureCoreWebView2Async（用热环境）");
            await WebView.EnsureCoreWebView2Async(_env);
            App.Log("WebView2 控件就绪");

            // 注册消息处理（对应 Photino 的 RegisterWebMessageReceivedHandler）
            WebView.CoreWebView2.WebMessageReceived += (s, args) =>
            {
                try
                {
                    string msg = args.TryGetWebMessageAsString();
                    HandleWebMessage(msg);
                }
                catch { }
            };

            // 注入 helper 脚本（和 Photino 版保持一致的接口）
            string injectScript = @"
<script>
function send(action, data) {
    var obj = { action: action };
    if (data) for (var k in data) obj[k] = data[k];
    window.chrome.webview.postMessage(JSON.stringify(obj));
}
window.chrome.webview.addEventListener('message', function(e) {
    try { eval(e.data); } catch (err) { console.error(err); }
});
</script>";

            string finalHtml = _payload.html + injectScript;
            WebView.CoreWebView2.NavigateToString(finalHtml);
            App.Log("NavigateToString 完成");

            // 显示窗口
            this.Title = _payload.title;
            this.Width = _payload.width;
            this.Height = _payload.height;
            this.Show();
            this.Activate();
            App.Log("窗口已显示");
        }

        /// <summary>
        /// 对应 Photino 版里的 "SendWebMessage 到 LogicHost.Handle"
        /// </summary>
        private void HandleWebMessage(string message)
        {
            if (!_logicLoaded) return;

            try
            {
                string js = _logicHost.Handle(
                    message,
                    executeJs: (s) => { try { WebView.CoreWebView2.PostWebMessageAsString(s); } catch { } },
                    launchTarget: () => LaunchTarget(_payload.target!),
                    exitApp: () => Environment.Exit(0));

                if (!string.IsNullOrEmpty(js))
                {
                    try { WebView.CoreWebView2.PostWebMessageAsString(js); } catch { }
                }
            }
            catch { }
        }

        private void LaunchTarget(byte[] targetBytes)
        {
            string tmpDir = Path.Combine(Path.GetTempPath(),
                "ShellApp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            string tmpExe = Path.Combine(tmpDir, "target.exe");
            File.WriteAllBytes(tmpExe, targetBytes);

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
