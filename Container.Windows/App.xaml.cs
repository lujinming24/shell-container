using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Web.WebView2.Core;

namespace Container
{
    public partial class App : Application
    {
        static Stopwatch _sw = new Stopwatch();

        public static void Log(string msg)
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "container_timing.log");
                File.AppendAllText(path,
                    DateTime.Now.ToString("HH:mm:ss.fff") +
                    "  +" + _sw.ElapsedMilliseconds + "ms  " +
                    msg + "\r\n");
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "container_timing.log");
                File.Delete(path);
            }
            catch { }

            _sw.Start();
            Log("=== App.OnStartup 开始 ===");

            // ★ 并发：
            //   任务 1：立刻预热 WebView2 环境
            //   任务 2：读 payload + 加载 Logic.dll
            var envTask = Task.Run(async () =>
            {
                Log("预热：开始创建 WebView2 环境");
                var env = await CoreWebView2Environment.CreateAsync(null, null, null);
                Log("预热：WebView2 环境创建完成");
                return env;
            });

            var payloadTask = Task.Run(() =>
            {
                Log("并发：开始读 payload");
                var p = Payload.ReadSelf();
                Log("并发：payload 读取完成 target=" + (p.target?.Length ?? 0) +
                    " html=" + (p.html?.Length ?? 0) +
                    " logic=" + (p.logicDll?.Length ?? 0) +
                    " wv2=" + (p.wv2?.Length ?? 0));

                var host = new LogicHost();
                bool ok = host.Load(p.logicDll);
                Log("并发：Logic.dll 加载完成 loaded=" + ok);

                return (p, host, ok);
            });

            Task.WhenAll(envTask, payloadTask).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Log("任务失败: " + t.Exception?.Message);
                    MessageBox.Show("初始化失败：" + t.Exception?.Message);
                    Shutdown();
                    return;
                }

                Dispatcher.Invoke(() =>
                {
                    var env = envTask.Result;
                    var (payload, host, logicLoaded) = payloadTask.Result;

                    Log("全部就绪，创建 ShellWindow");

                    var win = new ShellWindow(env, payload, host, logicLoaded);
                    MainWindow = win;
                    // ShellWindow 自己在 WebView2 就绪 + HTML 加载后 Show()
                });
            });
        }
    }
}
