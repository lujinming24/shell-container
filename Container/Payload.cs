using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Container
{
    public class Payload
    {
        public byte[]? target;
        public string? html;
        public string title = "Loading";
        public int width = 900;
        public int height = 600;

        private static readonly byte[] MAGIC = Encoding.ASCII.GetBytes("SHELLPACK_V1");

        public static Payload ReadSelf()
        {
            var p = new Payload();
            string self = Process.GetCurrentProcess().MainModule!.FileName;
            if (!File.Exists(self)) return p;

            byte[] all = File.ReadAllBytes(self);
            int total = all.Length;

            // 最小版元数据：MAGIC(12) + tLen(8) + hLen(8) + 宽(4) + 高(4) = 36 字节
            if (total < 36) return p;

            int mp = total - 36;
            for (int i = 0; i < 12; i++)
                if (all[mp + i] != MAGIC[i]) return p;

            long tLen = BitConverter.ToInt64(all, mp + 12);
            long hLen = BitConverter.ToInt64(all, mp + 20);
            int w = BitConverter.ToInt32(all, mp + 28);
            int h = BitConverter.ToInt32(all, mp + 32);

            if (tLen <= 0 || hLen < 0) return p;
            if (36 + tLen + hLen > total) return p;

            if (w < 100 || w > 10000) w = 900;
            if (h < 100 || h > 10000) h = 600;

            long dataStart = total - 36 - tLen - hLen;

            p.target = new byte[tLen];
            Array.Copy(all, dataStart, p.target, 0, tLen);

            p.html = Encoding.UTF8.GetString(all, (int)(dataStart + tLen), (int)hLen);

            p.width = w;
            p.height = h;
            return p;
        }
    }
}
