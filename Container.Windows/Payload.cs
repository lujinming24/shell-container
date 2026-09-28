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
        public byte[]? logicDll;
        public byte[]? wv2;
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

            if (total < 60) return p;

            int mp = total - 60;
            for (int i = 0; i < 12; i++)
                if (all[mp + i] != MAGIC[i]) return p;

            long tLen     = BitConverter.ToInt64(all, mp + 12);
            long hLen     = BitConverter.ToInt64(all, mp + 20);
            long logicLen = BitConverter.ToInt64(all, mp + 28);
            long wv2Len   = BitConverter.ToInt64(all, mp + 36);
            long titleLen = BitConverter.ToInt64(all, mp + 44);
            int w = BitConverter.ToInt32(all, mp + 52);
            int h = BitConverter.ToInt32(all, mp + 56);

            if (tLen <= 0 || hLen < 0 || logicLen < 0 || wv2Len < 0 || titleLen < 0)
                return p;
            if (60 + tLen + hLen + logicLen + wv2Len + titleLen > total)
                return p;

            if (w < 100 || w > 10000) w = 900;
            if (h < 100 || h > 10000) h = 600;

            long dataStart = total - 60 - tLen - hLen - logicLen - wv2Len - titleLen;

            p.target = new byte[tLen];
            Array.Copy(all, dataStart, p.target, 0, tLen);

            p.html = Encoding.UTF8.GetString(all, (int)(dataStart + tLen), (int)hLen);

            p.logicDll = new byte[logicLen];
            if (logicLen > 0)
                Array.Copy(all, dataStart + tLen + hLen, p.logicDll, 0, logicLen);

            p.wv2 = new byte[wv2Len];
            if (wv2Len > 0)
                Array.Copy(all, dataStart + tLen + hLen + logicLen, p.wv2, 0, wv2Len);

            if (titleLen > 0)
            {
                p.title = Encoding.UTF8.GetString(
                    all,
                    (int)(dataStart + tLen + hLen + logicLen + wv2Len),
                    (int)titleLen);
            }

            p.width = w;
            p.height = h;
            return p;
        }
    }
}
