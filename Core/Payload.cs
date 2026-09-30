using System;
using System.IO;
using System.Text;

namespace ShellContainer
{
    /// <summary>
    /// 壳数据解析。
    /// 布局：[容器][目标][HTML][Logic.dll][WV2安装器][标题][入口名(V2)][尾部元数据]
    /// 尾部元数据：
    ///   V1(60B) = MAGIC(12) + tLen(8) + hLen(8) + logicLen(8) + wv2Len(8) + titleLen(8) + w(4) + h(4)
    ///   V2(68B) = V1 + entryNameLen(8)，MAGIC = "SHELLPACK_V2"
    /// </summary>
    public sealed class Payload
    {
        public byte[] Target = Array.Empty<byte>();
        public string Html = "";
        public byte[] Logic = Array.Empty<byte>();
        public long Wv2InstallerOffset;
        public long Wv2InstallerLen;
        public string Title = "Shell";
        public int Width = 900;
        public int Height = 600;
        public bool IsDirectory;
        public string EntryName = "";
        public string ExePath = "";

        /// <summary>仅当 Windows 缺 WebView2 时调用，避免每次启动读大文件。</summary>
        public byte[] ReadInstaller()
        {
            if (Wv2InstallerLen <= 0) return Array.Empty<byte>();
            using var fs = File.OpenRead(ExePath);
            fs.Seek(Wv2InstallerOffset, SeekOrigin.Begin);
            var buf = new byte[Wv2InstallerLen];
            ReadExact(fs, buf);
            return buf;
        }

        public static Payload ReadFromSelf()
        {
            var exePath = Environment.ProcessPath
                ?? throw new InvalidOperationException("无法获取当前可执行文件路径");

            using var fs = File.OpenRead(exePath);
            long fileLen = fs.Length;

            // 读末尾 12 字节魔数
            fs.Seek(-12, SeekOrigin.End);
            Span<byte> magicBuf = stackalloc byte[12];
            ReadExact(fs, magicBuf);
            string magic = Encoding.ASCII.GetString(magicBuf);

            bool isDir;
            int trailerLen;
            if (magic == "SHELLPACK_V2") { isDir = true; trailerLen = 68; }
            else if (magic == "SHELLPACK_V1") { isDir = false; trailerLen = 60; }
            else throw new InvalidOperationException("无效的壳数据：魔数不匹配");

            // 读完整元数据
            fs.Seek(-trailerLen, SeekOrigin.End);
            var trailer = new byte[trailerLen];
            ReadExact(fs, trailer);

            int o = 12;
            long targetLen = BitConverter.ToInt64(trailer, o); o += 8;
            long htmlLen = BitConverter.ToInt64(trailer, o); o += 8;
            long logicLen = BitConverter.ToInt64(trailer, o); o += 8;
            long wv2Len = BitConverter.ToInt64(trailer, o); o += 8;
            long titleLen = BitConverter.ToInt64(trailer, o); o += 8;
            long entryNameLen = 0;
            if (isDir) { entryNameLen = BitConverter.ToInt64(trailer, o); o += 8; }
            int width = BitConverter.ToInt32(trailer, o); o += 4;
            int height = BitConverter.ToInt32(trailer, o); o += 4;

            long containerLen = fileLen - trailerLen - targetLen - htmlLen - logicLen
                              - wv2Len - titleLen - entryNameLen;
            if (containerLen < 0) throw new InvalidOperationException("壳数据长度校验失败");

            long pos = containerLen;
            var target = ReadRange(fs, pos, targetLen); pos += targetLen;
            var html = Encoding.UTF8.GetString(ReadRange(fs, pos, htmlLen)); pos += htmlLen;
            var logic = ReadRange(fs, pos, logicLen); pos += logicLen;
            long wv2Offset = pos; pos += wv2Len;
            var title = Encoding.UTF8.GetString(ReadRange(fs, pos, titleLen)); pos += titleLen;
            var entryName = entryNameLen > 0
                ? Encoding.UTF8.GetString(ReadRange(fs, pos, entryNameLen)) : "";

            return new Payload
            {
                Target = target,
                Html = html,
                Logic = logic,
                Wv2InstallerOffset = wv2Offset,
                Wv2InstallerLen = wv2Len,
                Title = string.IsNullOrEmpty(title) ? "Shell" : title,
                Width = width < 100 ? 900 : width,
                Height = height < 100 ? 600 : height,
                IsDirectory = isDir,
                EntryName = entryName,
                ExePath = exePath,
            };
        }

        private static byte[] ReadRange(FileStream fs, long offset, long length)
        {
            if (length <= 0) return Array.Empty<byte>();
            fs.Seek(offset, SeekOrigin.Begin);
            var buf = new byte[length];
            ReadExact(fs, buf);
            return buf;
        }

        private static void ReadExact(FileStream fs, Span<byte> buffer)
        {
            int read = 0;
            while (read < buffer.Length)
            {
                int n = fs.Read(buffer[read..]);
                if (n <= 0) break;
                read += n;
            }
        }
    }
}
