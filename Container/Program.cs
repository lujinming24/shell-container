using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Photino.NET;

namespace Container
{
    class Program
    {
        static Stopwatch _sw = new Stopwatch();

        static void Log(string msg)
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

        [STAThread]
        static void Main(string[] args)
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
            Log("=== 程序开始 ===");

            // 最小版：直接读 payload
            Log("开始读 payload");
            var payload = Payload.ReadSelf();
            Log("payload 读取完成 target=" + (payload.target?.Length ?? 0) +
                " html=" + (payload.html?.Length ?? 0));

            if (payload.target == null || payload.html == null)
            {
                Log("无壳数据，弹提示");
                var w = new PhotinoWindow()
                    .SetTitle("Container")
                    .SetUseOsDefaultSize(false)
                    .SetSize(400, 200)
                    .Center();
                w.SetLogVerbosity(0);
                w.LoadRawString("<h1>无壳数据</h1>");
                w.WaitForClose();
                return;
            }

            Log("准备创建 Photino 窗口");

            var window = new PhotinoWindow()
                .SetTitle(payload.title)
                .SetUseOsDefaultSize(false)
                .SetSize(payload.width, payload.height)
                .Center();

            Log("PhotinoWindow 构造完成");

            window.SetLogVerbosity(0);
            Log("SetLogVerbosity 完成");

            Log("准备 LoadRawString");
            window.LoadRawString(payload.html);
            Log("LoadRawString 完成");

            Log("准备进入 WaitForClose");
            window.WaitForClose();
            Log("=== 程序退出 ===");
        }
    }
}
