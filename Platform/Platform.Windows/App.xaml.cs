using System;
using System.Threading.Tasks;
using System.Windows;
using LogicLib;
using Microsoft.Web.WebView2.Core;

namespace ShellContainer
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ============ 1. 前置依赖探测 ============
            if (!WebView2Runtime.EnsureInstalled())
            {
                // 安装完需要重启容器
                return;
            }

            // ============ 2. 读壳数据（只读必要段）============
            Payload payload;
            try
            {
                payload = Payload.ReadFromSelf();
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取壳数据失败：" + ex.Message);
                Shutdown();
                return;
            }

            if (payload.Target.Length == 0 || string.IsNullOrEmpty(payload.Html))
            {
                MessageBox.Show("壳数据无效");
                Shutdown();
                return;
            }

            // ============ 3. 并发：预热 WebView2 环境 + 加载 Logic ============
            var envTask = Task.Run(async () =>
            {
                try
                {
                    return await CoreWebView2Environment.CreateAsync(null, null, null);
                }
                catch
                {
                    return null;
                }
            });

            var logicTask = Task.Run(() =>
            {
                try
                {
                    return (ILogic?)LogicBridge.Load(payload.Logic);
                }
                catch
                {
                    return null;
                }
            });

            Task.WhenAll(envTask, logicTask).ContinueWith(t =>
            {
                Dispatcher.Invoke(() =>
                {
                    var env = envTask.Result;
                    var logic = logicTask.Result;

                    if (env == null)
                    {
                        MessageBox.Show("WebView2 初始化失败");
                        Shutdown();
                        return;
                    }

                    // ============ 4. 创建 ShellHost 并启动 ============
                    var host = new WindowsShellHost(env);
                    host.CreateWindow(payload.Width, payload.Height, payload.Title);

                    // 绑定 Logic 消息处理
                    if (logic != null)
                    {
                        var launcher = new TargetLauncher(payload);
                        host.MessageReceived += (msg) =>
                        {
                            try
                            {
                                var ctx = new LogicContext
                                {
                                    ExecuteJs = js => host.SendMessage(js),
                                    LaunchTarget = () => launcher.Launch(),
                                    ExitApp = () => host.CloseWindow(),
                                };
                                string js = logic.HandleMessage(msg, ctx);
                                if (!string.IsNullOrEmpty(js))
                                    host.SendMessage(js);
                            }
                            catch (Exception ex)
                            {
                                host.SendMessage("console.error(" +
                                    System.Text.Json.JsonSerializer.Serialize(ex.Message) + ");");
                            }
                        };
                    }

                    // 后台初始化 WebView
                    host.InitWebView();

                    // 渲染完成 → 显示窗口
                    host.RenderCompleted += () =>
                    {
                        host.ShowWindow();
                    };

                    // 加载 HTML（异步）
                    string inject = WindowsInjectScript.Build();
                    host.LoadHtml(payload.Html + inject);

                    // 进入消息循环
                    host.Run();
                });
            });
        }
    }
}
