using System;
using Gtk;
using WebKit;

namespace ShellContainer
{
    public sealed class LinuxShellHost : IShellHost
    {
        private Gtk.Window? _window;
        private WebKit.WebView? _webView;
        private WebKit.UserContentManager? _contentManager;

        public event Action<string>? MessageReceived;
        public event Action? RenderCompleted;

        public static bool IsWebKitAvailable()
        {
            try
            {
                // 用 dlopen 试探加载 libwebkit2gtk-4.1
                var lib = System.Runtime.InteropServices.NativeLibrary.TryLoad(
                    "libwebkit2gtk-4.1.so.0", out _);
                return lib;
            }
            catch
            {
                return false;
            }
        }

        public void CreateWindow(int width, int height, string title)
        {
            Application.Init();

            _window = new Gtk.Window(title);
            _window.SetDefaultSize(
                Math.Max(width, 320),
                Math.Max(height, 240));
            _window.DeleteEvent += (o, args) =>
            {
                Application.Quit();
                args.RetVal = true;
            };

            // 创建 WebView（暂不显示窗口）
            _contentManager = new WebKit.UserContentManager();
            _contentManager.ScriptMessageReceived += OnScriptMessage;
            _contentManager.RegisterScriptMessageHandler("shell");

            var settings = new WebKit.Settings();
            _webView = new WebKit.WebView
            {
                Settings = settings,
                // 关联 ContentManager（取决于 WebKit2GtkSharp 版本 API）
            };

            _webView.LoadChanged += OnLoadChanged;

            _window.Add(_webView);
            // 不 ShowAll()，窗口不显示
        }

        public void InitWebView()
        {
            // WebView 已创建，无额外初始化
        }

        public void LoadHtml(string html)
        {
            if (_webView == null) return;
            _webView.LoadHtml(html, "about:blank");
        }

        public void ShowWindow()
        {
            if (_window == null) return;
            _window.ShowAll();
        }

        public void CloseWindow()
        {
            if (_window == null) return;
            _window.Destroy();
        }

        public void SendMessage(string js)
        {
            if (_webView == null) return;
            // 用 RunJavascript 执行
            string wrapped = "(function(){ try { eval(" +
                System.Text.Json.JsonSerializer.Serialize(js) +
                "); } catch(e) { console.error(e); } })();";
            _webView.RunJavascript(wrapped);
        }

        public void Run()
        {
            Application.Run();
        }

        // ============ 内部回调 ============

        private void OnScriptMessage(object? sender, ScriptMessageReceivedArgs args)
        {
            var body = args.JsValue?.ToString() ?? "";
            MessageReceived?.Invoke(body);
        }

        private void OnLoadChanged(object? sender, LoadChangedArgs args)
        {
            if (args.LoadEvent == LoadEvent.Finished)
            {
                RenderCompleted?.Invoke();
            }
        }
    }
}
