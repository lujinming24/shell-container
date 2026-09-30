using System;
using LogicLib;

namespace ShellContainer
{
    internal static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // ============ 1. 前置依赖探测 ============
            if (!LinuxShellHost.IsWebKitAvailable())
            {
                Console.Error.WriteLine(
                    "缺少系统依赖 libwebkit2gtk-4.1。请安装：\n" +
                    "  Debian/Ubuntu: sudo apt install libwebkit2gtk-4.1-0\n" +
                    "  Fedora:        sudo dnf install webkit2gtk4.1\n" +
                    "  Arch:          sudo pacman -S webkit2gtk-4.1");
                return 1;
            }

            // ============ 2. 读壳数据 ============
            Payload payload;
            try
            {
                payload = Payload.ReadFromSelf();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("读取壳数据失败: " + ex.Message);
                return 1;
            }

            if (payload.Target.Length == 0 || string.IsNullOrEmpty(payload.Html))
            {
                Console.Error.WriteLine("壳数据无效");
                return 1;
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
            var host = new LinuxShellHost();

            host.CreateWindow(payload.Width, payload.Height, payload.Title);

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

            host.InitWebView();

            host.RenderCompleted += () =>
            {
                host.ShowWindow();
            };

            string inject = LinuxInjectScript.Build();
            host.LoadHtml(payload.Html + inject);

            host.Run();

            return 0;
        }
    }
}
