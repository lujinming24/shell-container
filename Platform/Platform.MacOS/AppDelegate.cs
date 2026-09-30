using System;
using AppKit;
using Foundation;
using LogicLib;

namespace ShellContainer
{
    public class AppDelegate : NSApplicationDelegate
    {
        private MacShellHost? _host;

        public override void DidFinishLaunching(NSNotification notification)
        {
            // ============ 1. 前置依赖探测 ============
            // macOS 系统原生自带 WKWebView，不需要安装
            // 只需要确认 WKWebView 类存在（<5ms）
            if (!MacShellHost.IsWKWebViewAvailable())
            {
                ShowError("系统缺少 WKWebView（不应该发生）");
                NSApplication.SharedApplication.Terminate(null);
                return;
            }

            // ============ 2. 读壳数据 ============
            Payload payload;
            try
            {
                payload = Payload.ReadFromSelf();
            }
            catch (Exception ex)
            {
                ShowError("读取壳数据失败：" + ex.Message);
                NSApplication.SharedApplication.Terminate(null);
                return;
            }

            if (payload.Target.Length == 0 || string.IsNullOrEmpty(payload.Html))
            {
                ShowError("壳数据无效");
                NSApplication.SharedApplication.Terminate(null);
                return;
            }

            // ============ 3. 加载 Logic.dll ============
            ILogic? logic = null;
            try
            {
                logic = LogicBridge.Load(payload.Logic);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("加载 Logic.dll 失败: " + ex.Message);
            }

            // ============ 4. 创建 ShellHost ============
            _host = new MacShellHost();

            // 创建窗口（不显示）
            _host.CreateWindow(payload.Width, payload.Height, payload.Title);

            // 绑定消息处理
            if (logic != null)
            {
                var launcher = new TargetLauncher(payload);
                _host.MessageReceived += (msg) =>
                {
                    try
                    {
                        var ctx = new LogicContext
                        {
                            ExecuteJs = js => _host.SendMessage(js),
                            LaunchTarget = () => launcher.Launch(),
                            ExitApp = () => _host.CloseWindow(),
                        };
                        string js = logic.HandleMessage(msg, ctx);
                        if (!string.IsNullOrEmpty(js))
                            _host.SendMessage(js);
                    }
                    catch (Exception ex)
                    {
                        _host.SendMessage("console.error(" +
                            System.Text.Json.JsonSerializer.Serialize(ex.Message) + ");");
                    }
                };
            }

            // 后台初始化 WebView
            _host.InitWebView();

            // 渲染完成 → 显示窗口
            _host.RenderCompleted += () =>
            {
                _host.ShowWindow();
            };

            // 加载 HTML
            string inject = MacInjectScript.Build();
            _host.LoadHtml(payload.Html + inject);
        }

        public override bool ApplicationShouldTerminateAfterLastWindowClosed(NSApplication sender)
        {
            return true;
        }

        private void ShowError(string msg)
        {
            var alert = new NSAlert
            {
                MessageText = "错误",
                InformativeText = msg,
                AlertStyle = NSAlertStyle.Critical
            };
            alert.AddButton("确定");
            alert.RunModal();
        }
    }
}
