using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace HtmlPreviewer
{
    public enum TargetPlatform
    {
        Windows,
        MacOS,
        Linux
    }

    public static class Packer
    {
        private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("SHELLPACK_V1");

        public static void Build(string outputExe, string targetExe, string html,
                                  TargetPlatform platform, int width = 900, int height = 600)
        {
            if (width < 100 || width > 10000) width = 900;
            if (height < 100 || height > 10000) height = 600;

            string resourceName = platform switch
            {
                TargetPlatform.Windows => "HtmlPreviewer.Resources.container.exe",
                TargetPlatform.MacOS => "HtmlPreviewer.Resources.container-mac",
                TargetPlatform.Linux => "HtmlPreviewer.Resources.container-linux",
                _ => throw new ArgumentException("未知平台")
            };

            var asm = Assembly.GetExecutingAssembly();
            byte[] containerBytes;
            using (var s = asm.GetManifestResourceStream(resourceName)
                            ?? throw new Exception("找不到嵌入资源：" + resourceName))
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                containerBytes = ms.ToArray();
            }

            byte[] targetBytes = File.ReadAllBytes(targetExe);
            byte[] htmlBytes = Encoding.UTF8.GetBytes(html);

            // 格式：[容器][目标][HTML][MAGIC(12)][目标长度(8)][HTML长度(8)][宽(4)][高(4)]
            using (var fs = new FileStream(outputExe, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(containerBytes);
                bw.Write(targetBytes);
                bw.Write(htmlBytes);
                bw.Write(MAGIC);
                bw.Write((long)targetBytes.Length);
                bw.Write((long)htmlBytes.Length);
                bw.Write(width);
                bw.Write(height);
            }
        }
    }
}
