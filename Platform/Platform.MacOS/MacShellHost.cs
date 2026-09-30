using System;
using AppKit;
using Foundation;
using WebKit;
using ObjCRuntime;

namespace ShellContainer
{
    /// <summary>macOS 平台：WKWebView。</summary>
    public sealed class MacShellHost : IShellHost
    {
        private NSWindow? _window;
        private WKWebView? _webView;
        private WKUserContentController? _userContentController;
        private ShellMessageHandler? _messageHandler;

        public event Action<string>? MessageReceived;
        public event Action? RenderCompleted;

        public static bool IsWKWebViewAvailable()
        {
            return Class.GetHandle("WKWebView") != IntPtr.Zero;
        }

        public void CreateWindow(int width, int height, string title)
        {
            var contentRect = new CoreGraphics.CGRect(0, 0, width, height);
            var style = NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Resizable;

            // 创建 NSWindow，不显示
            _window = new NSWindow(contentRect, style, NSBackingStore.Buffered, false)
            {
                Title = title,
                ReleasedWhenClosed = false
            };
            _window.Center();

            // 创建 WKWebView
            var config = new WKWebViewConfiguration();
            _userContentController = new WKUserContentController();
            config.UserContentController = _userContentController;

            _messageHandler = new ShellMessageHandler(this);
            _userContentController.AddScriptMessageHandler(_messageHandler, "shell");

            _webView = new WKWebView(contentRect, config)
            {
                NavigationDelegate = new ShellNavigationDelegate(this)
            };

            _window.ContentView = _webView;
            // 窗口未显示
        }

        public void InitWebView()
        {
            // WKWebView 在 CreateWindow 时已创建，不需要额外初始化
            // 但为了和 Windows 版接口一致，这里保留空实现
        }

        public void LoadHtml(string html)
        {
            if (_webView == null) return;
            _webView.LoadHtmlString(html, null);
        }

        public void ShowWindow()
        {
            if (_window == null) return;
            _window.MakeKeyAndOrderFront(null);
            NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
        }

        public void CloseWindow()
        {
            if (_window == null) return;
            _window.Close();
        }

        public void SendMessage(string js)
        {
            if (_webView == null) return;
            // 用 evaluateJavaScript 执行 JS（包含 eval 逻辑，由注入脚本处理）
            _webView.EvaluateJavaScript(js, null);
        }

        public void Run()
        {
            // macOS 用 NSApplication.Run()，已在 Program.cs 里启动
        }

        // ============ 内部回调 ============

        internal void OnMessageReceived(string msg)
        {
            MessageReceived?.Invoke(msg);
        }

        internal void OnNavigationCompleted(bool success)
        {
            if (success)
                RenderCompleted?.Invoke();
        }
    }

    /// <summary>JS → C# 的消息处理器。</summary>
    public class ShellMessageHandler : NSObject, IWKScriptMessageHandler
    {
        private readonly MacShellHost _host;
        public ShellMessageHandler(MacShellHost host) => _host = host;

        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
        {
            var body = message.Body?.ToString() ?? "";
            _host.OnMessageReceived(body);
        }
    }

    /// <summary>WKWebView 导航事件代理。</summary>
    public class ShellNavigationDelegate : NSObject, IWKNavigationDelegate
    {
        private readonly MacShellHost _host;
        public ShellNavigationDelegate(MacShellHost host) => _host = host;

        public override void DidFinishNavigation(WKWebView webView, WKNavigation? navigation)
        {
            _host.OnNavigationCompleted(true);
        }

        public override void DidFailNavigation(WKWebView webView, WKNavigation? navigation, NSError error)
        {
            _host.OnNavigationCompleted(false);
        }
    }
}
