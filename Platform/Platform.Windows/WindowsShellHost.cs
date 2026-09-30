using System;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace ShellContainer
{
    /// <summary>Windows 平台：WPF + WebView2。</summary>
    public sealed class WindowsShellHost : IShellHost
    {
        private readonly CoreWebView2Environment _env;
        private ShellWindow? _window;

        public event Action<string>? MessageReceived;
        public event Action? RenderCompleted;

        public WindowsShellHost(CoreWebView2Environment env)
        {
            _env = env;
        }

        public void CreateWindow(int width, int height, string title)
        {
            _window = new ShellWindow
            {
                Title = title,
                Width = Math.Max(width, 320),
                Height = Math.Max(height, 240),
            };
            // 窗口未显示（XAML 里 Visibility="Hidden"）
        }

        public void InitWebView()
        {
            if (_window == null) return;

            // 用已预热的环境初始化 WebView2 控件
            _window.Loaded += async (s, e) =>
            {
                var wv = _window.WebViewRaw.Control;
                await wv.EnsureCoreWebView2Async(_env);

                // 注册 JS → C# 消息
                wv.CoreWebView2.WebMessageReceived += (sender, args) =>
                {
                    try
                    {
                        string msg = args.TryGetWebMessageAsString();
                        MessageReceived?.Invoke(msg);
                    }
                    catch { }
                };

                // 监听渲染完成
                wv.CoreWebView2.NavigationCompleted += (sender, args) =>
                {
                    if (args.IsSuccess)
                    {
                        // 渲染完成，通知外层显示窗口
                        RenderCompleted?.Invoke();
                    }
                };
            };
        }

        public void LoadHtml(string html)
        {
            if (_window == null) return;
            var wv = _window.WebViewRaw.Control;
            // 等控件就绪后加载
            wv.Dispatcher.Invoke(() =>
            {
                // EnsureCoreWebView2Async 完成后，CoreWebView2 已就绪
                if (wv.CoreWebView2 != null)
                {
                    wv.CoreWebView2.NavigateToString(html);
                }
                else
                {
                    // 兜底：等 Loaded 后再调
                    wv.Loaded += (s, e) =>
                    {
                        wv.CoreWebView2?.NavigateToString(html);
                    };
                }
            });
        }

        public void ShowWindow()
        {
            if (_window == null) return;
            _window.Dispatcher.Invoke(() =>
            {
                _window.Visibility = Visibility.Visible;
                _window.Activate();
            });
        }

        public void CloseWindow()
        {
            if (_window == null) return;
            _window.Dispatcher.Invoke(() =>
            {
                _window.Close();
            });
        }

        public void SendMessage(string js)
        {
            if (_window == null) return;
            var wv = _window.WebViewRaw.Control;
            wv.Dispatcher.Invoke(() =>
            {
                try { wv.CoreWebView2?.PostWebMessageAsString(js); } catch { }
            });
        }

        public void Run()
        {
            // WPF 应用本身就是消息循环，无需额外 Run
        }
    }
}
